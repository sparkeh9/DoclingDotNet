# C ABI Bridge (`docling_parse_c`)

## Intent
Provide a stable non-Python interop boundary for .NET and other FFI consumers.

## Core contract
- Library target: `docling_parse_c`
- Header: `upstream/deps/docling-parse/src/c_api/docling_parse_c_api.h`
- Implementation: `upstream/deps/docling-parse/src/c_api/docling_parse_c_api.cpp`
- Documentation: `upstream/deps/docling-parse/docs/c_abi.md`

## Stability features
- ABI version introspection:
  - `docling_parse_get_abi_version`
- Struct-size compatibility checks:
  - `docling_parse_get_decode_page_config_size`
  - `docling_parse_init_decode_page_config`
- Backward compatible default config helper:
  - `docling_parse_get_default_decode_page_config`

## Runtime considerations
- Caller owns returned strings; must call `docling_parse_free_string`.
- Last error is handle-owned and mutable across calls.
- Handle thread safety is not guaranteed; synchronize externally if needed.

## Build path
```powershell
cmake -S upstream/deps/docling-parse -B upstream/deps/docling-parse/build-cabi -DDOCLING_PARSE_BUILD_C_API=ON -DDOCLING_PARSE_BUILD_PYTHON_BINDINGS=OFF
cmake --build upstream/deps/docling-parse/build-cabi --config Release --target docling_parse_c
```

## .NET validation
- Spike project: `dotnet/examples/Spike.DoclingParseCAbi`
- Assertion command:
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\test-docling-parse-cabi-smoke.ps1 -SkipConfigure
```
