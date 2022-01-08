# Contributing to this project

This project welcomes contributions and suggestions. By contributing, you confirm that you have the right to, and actually do, grant us the rights to use your contribution. More information below.

Please feel free to contribute code, ideas, improvements, and patches - we've added some general guidelines and information below, and you can propose changes to this document in a pull request.

This project has adopted the [Contributor Covenant Code of Conduct](https://avanade.github.io/code-of-conduct/).

<br/>

## Rights to your contributions
By contributing to this project, you:
- Agree that you have authored 100% of the content;
- Agree that you have the necessary rights to the content;
- Agree that you have received the necessary permissions from your employer to make the contributions (if applicable);
- Agree that the content you contribute may be provided under the Project license(s);
- Agree that, if you did not author 100% of the content, the appropriate licenses and copyrights have been added along with any other necessary attribution.

<br/>

## Code of Conduct
This project, and people participating in it, are governed by our [code of conduct](https://avanade.github.io/code-of-conduct/). By taking part, we expect you to try your best to uphold this code of conduct. If you have concerns about unacceptable behaviour, please contact the community leaders responsible for enforcement at
[ospo@avanade.com](ospo@avanade.com).

<br/>

## Coding guidelines

The most general guideline is that we use all the VS default settings in terms of code formatting; if in doubt, follow the coding convention of the existing code base.
1. Use four spaces of indentation (no tabs).
2. Use `_camelCase` for private fields.
3. Avoid `this.` unless absolutely necessary.
4. Always specify member visibility, even if it's the default (i.e. `private string _foo;` not `string _foo;`).
5. Open-braces (`{`) go on a new line (an `if` with single-line statement does not need braces).
6. Use any language features available to you (expression-bodied members, throw expressions, tuples, etc.) as long as they make for readable, manageable code.
7. All methods and properties must include the [XML documentation comments](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/xmldoc/xml-documentation-comments). Private methods and properties only need to specifiy the [summary](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/xmldoc/summary) as a minimum.

For further guidance see ASP.NET Core [Engineering guidelines](https://github.com/aspnet/AspNetCore/wiki/Engineering-guidelines).

<br/>

## Tests

We use [`NUnit`](https://github.com/nunit/nunit) for all unit testing.

- Tests need to be provided for every bug/feature that is completed.
- Tests only need to be present for issues that need to be verified by QA (for example, not tasks).
- If there is a scenario that is far too hard to test there does not need to be a test for it.
- "Too hard" is determined by the team as a whole.

We understand there is more work to be performed in generating a higher level of code coverage; this technical debt is on the backlog.

<br/>

## Code reviews and checkins

To help ensure that only the highest quality code makes its way into the project, please submit all your code changes to GitHub as PRs. This includes runtime code changes, unit test updates, and updates to the end-to-end demo.

For example, sending a PR for just an update to a unit test might seem like a waste of time but the unit tests are just as important as the product code and as such, reviewing changes to them is also just as important. This also helps create visibility for your changes so that others can observe what is going on.

The advantages are numerous: improving code quality, more visibility on changes and their potential impact, avoiding duplication of effort, and creating general awareness of progress being made in various areas.