// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
    "Minor Code Smell",
    "S3267:Loops should be simplified with \"LINQ\" expressions",
    Justification = "Using Linq is not always more readable, programmer should evaluete the best way to itarate collection in case by case basis.",
    Scope = "module"
    )]

[assembly: SuppressMessage(
    "Performance",
    "CA1873:Avoid potentially expensive logging",
    Justification = "The application currently does not contain expensive logging scenarios that we do not want to trigger.",
    Scope = "module"
    )]

[assembly: SuppressMessage(
    "Major Code Smell",
    "S107:Methods should not have too many parameters",
    Justification = "Controller endpoints typically have long parameter lists due to query parameters.",
    Scope = "namespaceanddescendants",
    Target = "~N:PxApi.Controllers"
    )]
