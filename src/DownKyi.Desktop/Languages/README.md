# Desktop language resources

Each AXAML file is one complete UI language catalog. Its file name is the
locale selected by `DesktopLanguageController`; there is no language-neutral
`Default.axaml` catalog.

The four language choices display their native names directly in
`Views/Settings/ViewBasic.axaml`. Those names do not change with the selected
language and are not repeated in the catalogs.

Ordinary `DictionaryResource.GetString` calls report unavailable keys after
Avalonia initializes. Before that, headless presentation models have no UI
resources and retain their empty-string placeholder. Callers that need a
specific pre-startup message pass an explicit fallback.

Language changes intentionally take effect on the next application start. The
controller replaces the startup `zh-CN.axaml` fallback before views and view
models are created, so both XAML resources and strings materialized in C# use
one language for the lifetime of that process.

When changing localized UI text:

1. Add or remove the same key in every locale file at the same position.
2. Preserve every composite-format argument, such as `{0}` or `{1:N0}`.
3. Name keys for their UI responsibility instead of copying the displayed text.
4. Keep Simplified and Traditional Chinese changes in their respective
   `zh-CN.axaml` and `zh-TW.axaml` catalogs.
5. Add a focused presentation test when wording carries workflow semantics.

The architecture tests enforce the catalog file names, key order, format
arguments, literal C# lookup keys, AXAML resource references, and a
Traditional-script safety check for Simplified Chinese. The script check is a
heuristic; native speakers still need to review wording in context.
