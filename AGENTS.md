# Agent Guide for AI Backstage Pass conference session presentations

This session pulls the curtain back on what AI really does when you give it a task: how it succeeds, why it fails, and what you can do to control it. Through three multimodal use cases, we’ll compare naive prompts with refined workflows so you can see the difference structure makes.

You’ll learn:

- How to design tasks that avoid hallucinations
- How to combine modalities to increase accuracy
- How to turn unpredictable models into dependable features inside your .NET apps.

Real demos, real mistakes, real fixes.

These examples use:
- .NET 10
- ASP.NET Blazor Server Interactive

# Early development
Don't worry about migrations or backwards compatibility. I'd rather have it always be done the correct way from the ground up than shoehorning existing features.

## Engineering Standards
- No user questions are rhetorical, always stop and answer questions before proceeding with code changes.
- When the user states a rule that implies "always do this", add it cleanly to this file. Your memory doesn't persist unless you put it here.
- Always ask before removing user-facing functionality to resolve behavior issues.
- If a change would remove or degrade an existing feature that the user did not explicitly request to remove, stop and get user confirmation before making the change. Do not assume feature removal is acceptable.
- Never use `UriKind.Absolute` / `Uri.TryCreate(..., UriKind.Absolute, ...)` to validate an absolute URL. The only allowed check in this codebase is `StartsWith("https://")` or `StartsWith("http://")` (case-insensitive). Note: `UriKind.Absolute` may still be used to explicitly construct a `Uri` as absolute, but not as a "validation" mechanism.
- Use link syntax in Markdown with meaningful titles instead of inline code when referencing files
- Single-statement control structures MUST NOT use braces.
  If an `if`, `else`, `for`, `foreach`, or `while` body contains exactly one statement, omit {}.
  Braces are required only for multi-statement blocks.
- Fix root causes, never patch symptoms; avoid hacks
- If unsure how to identify an issue, add detailed debugging output and ask the user to reproduce the issue. It's cheaper to ask the user for help than to waste tokens trying things. 
- Code must be production-ready, tested, and secure; use xUnit for unit tests
- If uncertain or requirements are ambiguous, ask the user before proceeding
- Never manually edit project or solution files (.csproj, .sln, .slnx); use the dotnet CLI
- Keep code DRY, KISS, SOLID; aim for native Microsoft-level quality
- Breaking changes are fine and encouraged. We should be refactoring to make everything the best possible approach. No shoehorning.
- Always look for existing implementations to solutions, find ways to reuse and enhance existing, rather than reinventing. This keeps things DRY and behavior consistent, and more testable.
- Use C# 12 primary constructors
- Do NOT use ConfigureAwait(false)
- Always trust null annotations. If a type is not annotated as it can be null, do NOT null check it.

### CRITICAL: .NET CLI Commands Required
**NEVER manually edit `.csproj` or `Directory.Packages.props` files.** Always use dotnet CLI commands:

```bash
# Create new projects
dotnet new classlib -n ProjectName
dotnet new razorclasslib -n ProjectName
dotnet new xunit -n ProjectName

# Add to solution
dotnet sln add Path/To/Project.csproj

# Add package references
dotnet add ProjectPath package PackageName

# Add project references
dotnet add ProjectPath reference OtherProject.csproj
```

This rule is **MANDATORY** for all agents. No exceptions.
