#!/usr/bin/env node

// OpenAPI document validator for PxApi.
// Covers SKILL steps 3 (syntax/schema) and 5 (unused definitions).
// Usage: node validate-openapi.mjs <path-to-openapi-doc.json>

import SwaggerParser from "@apidevtools/swagger-parser";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";

const VALID_HTTP_METHODS = new Set([
  "get", "post", "put", "delete", "patch", "head", "options",
]);

const filePath = process.argv[2];
if (!filePath) {
  console.error("Usage: node validate-openapi.mjs <path-to-openapi-doc.json>");
  process.exit(1);
}

const absolutePath = resolve(filePath);
const issues = [];
const unused = { schemas: [], parameters: [], requestBodies: [], responses: [], securitySchemes: [] };
let doc;

// ── Step 3: Syntax & Schema Validation ──────────────────────────────────

// 3-a. Valid JSON
try {
  const raw = readFileSync(absolutePath, "utf-8");
  doc = JSON.parse(raw);
} catch (err) {
  console.error(`❌ Failed to parse JSON: ${err.message}`);
  process.exit(2);
}

// 3-b. OpenAPI version
if (!doc.openapi) {
  issues.push({ location: "root", problem: "Missing 'openapi' version field." });
} else if (!/^3\.\d+\.\d+$/.test(doc.openapi)) {
  issues.push({ location: "root.openapi", problem: `Unexpected OpenAPI version format: '${doc.openapi}'.` });
}

// 3-c. Required top-level fields
for (const field of ["info", "paths"]) {
  if (!doc[field]) {
    issues.push({ location: "root", problem: `Missing required top-level field '${field}'.` });
  }
}
if (doc.info) {
  for (const sub of ["title", "version"]) {
    if (!doc.info[sub]) {
      issues.push({ location: "root.info", problem: `Missing required field '${sub}'.` });
    }
  }
}

// 3-d / 3-e / 3-f. Path, operation, $ref, and response-code validation
const allRefs = [];

function collectRefs(obj, path) {
  if (obj === null || typeof obj !== "object") return;
  if (Array.isArray(obj)) {
    obj.forEach((item, i) => collectRefs(item, `${path}[${i}]`));
    return;
  }
  for (const [key, value] of Object.entries(obj)) {
    const childPath = `${path}.${key}`;
    if (key === "$ref" && typeof value === "string") {
      allRefs.push({ ref: value, location: childPath });
    }
    collectRefs(value, childPath);
  }
}

collectRefs(doc, "#");

// Resolve component map for $ref checking
function resolveLocalRef(ref) {
  if (!ref.startsWith("#/")) return undefined; // external ref, skip
  const parts = ref.slice(2).split("/");
  let current = doc;
  for (const part of parts) {
    if (current === undefined || current === null) return undefined;
    current = current[part];
  }
  return current;
}

// 3-e. Schema references
for (const { ref, location } of allRefs) {
  if (ref.startsWith("#/") && resolveLocalRef(ref) === undefined) {
    issues.push({ location, problem: `Broken $ref: '${ref}' does not resolve.` });
  }
}

// 3-d. Path structure & 3-f. Response codes
if (doc.paths) {
  for (const [pathKey, pathItem] of Object.entries(doc.paths)) {
    for (const [method, operation] of Object.entries(pathItem)) {
      if (method === "parameters" || method === "$ref" || method === "summary" || method === "description" || method === "servers") continue;

      if (!VALID_HTTP_METHODS.has(method)) {
        issues.push({ location: `paths.${pathKey}`, problem: `Invalid HTTP method '${method}'.` });
        continue;
      }

      if (!operation.operationId) {
        issues.push({ location: `paths.${pathKey}.${method}`, problem: "Missing 'operationId'." });
      }

      if (!operation.responses || Object.keys(operation.responses).length === 0) {
        issues.push({ location: `paths.${pathKey}.${method}`, problem: "No responses defined." });
      } else {
        for (const code of Object.keys(operation.responses)) {
          if (code !== "default" && !/^[1-5]\d{2}$/.test(code)) {
            issues.push({ location: `paths.${pathKey}.${method}.responses`, problem: `Invalid HTTP status code '${code}'.` });
          }
        }
      }

      if (operation.parameters) {
        for (const param of operation.parameters) {
          if (!param.$ref) {
            if (!param.name) issues.push({ location: `paths.${pathKey}.${method}.parameters`, problem: "Parameter missing 'name'." });
            if (!param.in) issues.push({ location: `paths.${pathKey}.${method}.parameters`, problem: `Parameter '${param.name || "?"}' missing 'in'.` });
            if (!param.schema && !param.content) issues.push({ location: `paths.${pathKey}.${method}.parameters`, problem: `Parameter '${param.name || "?"}' missing 'schema'.` });
          }
        }
      }
    }
  }
}

// Swagger-parser deep validation (dereference + validate)
let parserIssues = [];
try {
  await SwaggerParser.validate(absolutePath);
} catch (err) {
  parserIssues.push(err.message);
  if (err.details) {
    for (const detail of err.details) {
      parserIssues.push(`  - ${detail.path.join(".")}: ${detail.message}`);
    }
  }
}

// ── Step 5: Unused Definitions ──────────────────────────────────────────

function findUnusedComponents(componentType) {
  const components = doc.components?.[componentType];
  if (!components) return [];

  const definedNames = Object.keys(components);
  const prefix = `#/components/${componentType}/`;
  const referenced = new Set();

  // Collect direct $ref usage
  for (const { ref } of allRefs) {
    if (ref.startsWith(prefix)) {
      referenced.add(ref.slice(prefix.length));
    }
  }

  // For schemas, also check discriminator.mapping values
  if (componentType === "schemas") {
    collectDiscriminatorRefs(doc, referenced, prefix);
  }

  return definedNames.filter((name) => !referenced.has(name));
}

function collectDiscriminatorRefs(obj, referenced, prefix) {
  if (obj === null || typeof obj !== "object") return;
  if (Array.isArray(obj)) {
    obj.forEach((item) => collectDiscriminatorRefs(item, referenced, prefix));
    return;
  }
  if (obj.discriminator?.mapping) {
    for (const ref of Object.values(obj.discriminator.mapping)) {
      if (ref.startsWith(prefix)) {
        referenced.add(ref.slice(prefix.length));
      }
    }
  }
  for (const value of Object.values(obj)) {
    collectDiscriminatorRefs(value, referenced, prefix);
  }
}

unused.schemas = findUnusedComponents("schemas");
unused.parameters = findUnusedComponents("parameters");
unused.requestBodies = findUnusedComponents("requestBodies");
unused.responses = findUnusedComponents("responses");
unused.securitySchemes = findUnusedSecuritySchemes();

function collectSecurityRefsFromRequirements(securityRequirements, referenced) {
  for (const req of securityRequirements) {
    for (const name of Object.keys(req)) {
      referenced.add(name);
    }
  }
}

function collectOperationSecurityRefs(referenced) {
  if (!doc.paths) return;
  for (const pathItem of Object.values(doc.paths)) {
    for (const [method, operation] of Object.entries(pathItem)) {
      if (VALID_HTTP_METHODS.has(method) && operation.security) {
        collectSecurityRefsFromRequirements(operation.security, referenced);
      }
    }
  }
}

function findUnusedSecuritySchemes() {
  const schemes = doc.components?.securitySchemes;
  if (!schemes) return [];

  const definedNames = Object.keys(schemes);
  const referenced = new Set();

  if (doc.security) {
    collectSecurityRefsFromRequirements(doc.security, referenced);
  }
  collectOperationSecurityRefs(referenced);

  return definedNames.filter((name) => !referenced.has(name));
}

// ── Report ──────────────────────────────────────────────────────────────

const totalUnused =
  unused.schemas.length +
  unused.parameters.length +
  unused.requestBodies.length +
  unused.responses.length +
  unused.securitySchemes.length;

console.log("## OpenAPI Document Inspection Report\n");

// Syntax section
console.log("### Syntax & Schema Validation");
if (issues.length === 0 && parserIssues.length === 0) {
  console.log("- **Status**: ✅ Valid");
} else {
  console.log(`- **Status**: ❌ Issues Found (${issues.length + parserIssues.length})`);
  for (const issue of issues) {
    console.log(`  - \`${issue.location}\`: ${issue.problem}`);
  }
  for (const msg of parserIssues) {
    console.log(`  - swagger-parser: ${msg}`);
  }
}

// Unused definitions section
console.log("\n### Unused Definitions");
if (totalUnused === 0) {
  console.log("- **Status**: ✅ Clean");
} else {
  console.log(`- **Status**: ⚠️ Unused Definitions Found`);
  if (unused.schemas.length > 0) {
    console.log(`- **Unused schemas** (${unused.schemas.length}):`);
    unused.schemas.forEach((s) => console.log(`  - ${s}`));
  }
  if (unused.parameters.length > 0) {
    console.log(`- **Unused parameters** (${unused.parameters.length}):`);
    unused.parameters.forEach((s) => console.log(`  - ${s}`));
  }
  if (unused.requestBodies.length > 0) {
    console.log(`- **Unused request bodies** (${unused.requestBodies.length}):`);
    unused.requestBodies.forEach((s) => console.log(`  - ${s}`));
  }
  if (unused.responses.length > 0) {
    console.log(`- **Unused responses** (${unused.responses.length}):`);
    unused.responses.forEach((s) => console.log(`  - ${s}`));
  }
  if (unused.securitySchemes.length > 0) {
    console.log(`- **Unused security schemes** (${unused.securitySchemes.length}):`);
    unused.securitySchemes.forEach((s) => console.log(`  - ${s}`));
  }
}

// Summary
console.log("\n### Summary");
const pathCount = doc.paths ? Object.keys(doc.paths).length : 0;
const schemaCount = doc.components?.schemas ? Object.keys(doc.components.schemas).length : 0;
console.log(`- **OpenAPI version**: ${doc.openapi || "unknown"}`);
console.log(`- **Paths**: ${pathCount}`);
console.log(`- **Schemas**: ${schemaCount}`);
console.log(`- **$ref links validated**: ${allRefs.length}`);

if (issues.length > 0 || parserIssues.length > 0) {
  process.exit(1);
}
