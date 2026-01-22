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

## Branching & Worktrees

Before creating a new branch or worktree:
1. Ensure `main` has no uncommitted files
2. Ensure `main` is in sync with `origin/main` (pull latest changes)

**Brainstorming → Implementation flow** (using `superpowers:brainstorming`):
1. Brainstorm and design on main (no commits)
2. Create feature branch/worktree via `superpowers:using-git-worktrees`
3. Commit design doc (`docs/plans/YYYY-MM-DD-<topic>-design.md`) to feature branch
4. Create implementation plan via `superpowers:writing-plans`, commit to feature branch
5. Plans only reach main as part of the PR merge for that feature

## Central Package Management

Use CPM (Directory.Packages.props) for version management. Do not specify versions in .csproj files.

## Releases

Release new versions by creating and pushing a git tag:

```bash
git tag v0.1.x && git push origin v0.1.x
```
