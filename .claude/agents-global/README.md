# Global agents

Generic versions of the MedFlow agents, with project-specific details removed.

Install for all your repos (on your own machine):

```bash
mkdir -p ~/.claude/agents
cp .claude/agents-global/*.md ~/.claude/agents/
rm ~/.claude/agents/README.md
```

Restart Claude Code afterwards. A repo's own `.claude/agents/` version of an agent takes precedence over the user-level one with the same name.

`feature-builder` uses the `speckit-*` skills when a repo has them and falls back to a lightweight flow otherwise.
