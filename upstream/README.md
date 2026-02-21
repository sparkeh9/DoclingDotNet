# Upstream Sources (Untracked)

This directory isolates code cloned from upstream Docling-related repositories.

## Layout
- `upstream/docling/`: upstream Python Docling repository snapshot
- `upstream/deps/`: upstream dependency snapshots (including `docling-parse`)

## Tracking policy
- These cloned sources are intentionally ignored by the root git repository.
- Treat them as external references and build inputs, not as primary tracked code for this port repo.
