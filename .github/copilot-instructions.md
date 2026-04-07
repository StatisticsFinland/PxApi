Follow these guidelines when writing code:

Security:
- Always treat user input as untrusted and validate it against expected formats, lengths, and types before processing.
- Make sure user input never goes directly to file paths, database queries, or command execution without proper validation and sanitization.
- Make sure user input is never logged directly without proper sanitization.
- Do not ever write or copy anything that can be considered a secret (e.g., API keys, connection strings, passwords) in code comments, documentation, or logs.

Coding conventions:
- Always use explicit type names instead of 'var' for local variables.
- Prefer new() expressions for object instantiation when the type is clear from context. For collections, use [] initializer syntax where supported (e.g., List<int> numbers = [1, 2, 3];).
- Use collection initializers to both create and populate collections in a single statement.
  Example: List<int> numbers = [1, 2, 3]; instead of new List<int>() { 1, 2, 3 }.
- Declare local variables as const or readonly if they are not reassigned after initialization. This improves code clarity and prevents accidental modification.
- Always include XML documentation comments for public methods and classes, with the exception of test projects, classes and methods.
- Properties of a class should be declared in the order of their visibility: public, protected, internal, private.
- Properties should be declared before methods in a class.
- Use primary constructors for classes where applicable to simplify the class definition and initialization.
- Constructors should be declared before methods in a class.
- Methods should be declared in the order of their visibility: public, protected, internal, private.
- Inline comments should be used sparingly and only when necessary to explain complex logic or clarify intent.

Logging:
- When writing logging statements, use template strings for better performance and readability.
- If the logging statement is in a catch block, include the exception object in the log call to capture the stack trace and other details.
- Try to use logger scope to provide context for the logs.

Unit testing:
- Use NUnit as the unit testing framework.
- Use Moq for mocking dependencies in unit tests.
- Use the Assert.That() syntax for all assertions.
- When making multiple assertions in a single test, group them using `using (Assert.EnterMultipleScope()) { ... }`.

Other:
- When refactoring code, update the documentation comments to reflect the changes made.