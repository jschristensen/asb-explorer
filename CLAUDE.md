# Project Instructions for Claude

## TDD Approach

All new code follows **Test-Driven Development**:

1. **RED:** Write a failing test that describes the desired behavior
2. **Verify RED:** Run test, confirm it fails for the right reason
3. **GREEN:** Write minimal code to make the test pass
4. **Verify GREEN:** Run test, confirm it passes
5. **REFACTOR:** Clean up while keeping tests green
6. **Commit:** Commit working code with tests

**Test project:** `src/AsbExplorer.Tests/` (xUnit)

**Run tests:**
```bash
dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj
```

**Testability strategy for Views:**
- Extract pure logic (formatting, calculations) into testable helper classes
- Views remain thin wrappers around Terminal.Gui components
- Test the extracted logic, not the UI wiring

## Central Package Management

Use CPM (Directory.Packages.props) for version management. Do not specify versions in .csproj files.

## Releases

Release new versions by creating and pushing a git tag:

```bash
git tag v0.1.x && git push origin v0.1.x
```
