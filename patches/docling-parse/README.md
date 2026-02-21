# docling-parse upstream delta patches

This folder tracks local upstream `docling-parse` changes that are intentionally excluded from root git by `.gitignore`.

Use these scripts:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\export-docling-parse-upstream-delta.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\apply-docling-parse-upstream-delta.ps1 -CheckOnly
powershell -ExecutionPolicy Bypass -File .\scripts\apply-docling-parse-upstream-delta.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\update-docling-parse-upstream-baseline.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\fetch-latest-and-port-docling-parse.ps1
```

Baseline metadata:
- `upstream-baseline.json`
  - tracks upstream commit/ref and patch hash used by the current port baseline.

Current patch:
- `0001-docling-parse-cabi-foundation-and-segmented-runtime.patch`
  - C ABI foundation (`docling_parse_c`)
  - ABI handshake hardening (`init_decode_page_config`, ABI version query)
  - segmented runtime decode endpoint (`docling_parse_decode_segmented_page_json`)
  - CMake dependency and Windows build hardening changes needed for this workspace
