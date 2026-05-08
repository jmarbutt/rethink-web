# Issue Tracker: GitHub

Issues and PRDs for this repo live in GitHub Issues for `jmarbutt/OpusKit`. Use the `gh` CLI from the repo root.

## Conventions

- Create an issue with `gh issue create --title "..." --body "..."`
- Read an issue with `gh issue view <number> --comments`
- List issues with `gh issue list --state open --json number,title,body,labels,comments`
- Comment with `gh issue comment <number> --body "..."`
- Apply or remove labels with `gh issue edit <number> --add-label "..."` or `--remove-label "..."`
- Close with `gh issue close <number> --comment "..."`

When a skill says "publish to the issue tracker", create a GitHub issue.

When a skill says "fetch the relevant ticket", run `gh issue view <number> --comments`.
