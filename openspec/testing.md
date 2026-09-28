# Guidelines for Testing

## Unit Tests

- Tech stack: TUnit and TUnit.Mocks
- We are using TUinit assertions by default
- We are using fixtures where it makes sense and test setup can be shared

## Functional Tests

- Tech stack: TUnit with Playwright and TUnit.Aspire for env setup
- We are using TUinit assertions by default
- We are using data-testid to locate user input elements and other ui elements
- We are using page objects to encapsulate locators and view logic into reusable units
