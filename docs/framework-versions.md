# Majorsilence.Forms versions

`Directory.Packages.props` pins one exact published version (`MajorsilenceFormsVersion`). Each bump is its own commit and adds a
line here: the version, the date, what it unlocked, what it broke. CI never uses the framework checkout.

| Version | Date | Unlocked | Broke or still wrong | Shims removed |
|---|---|---|---|---|
| 26.3.0 | 2026-09-26 | The initial pin. Headless backend for UI tests, the Android and browser heads | Template Android head crashes (#288); text scale (#289); binding under trimming (#290); custom `OnPaint` units undocumented (#291) | none |
| 26.5.0 | 2026-10-01 | Everything in 26.4.0 (rounded rectangles, `Majorsilence.Forms.Mvvm`, `ICommand` on buttons, animation, reduced motion, audio, lifecycle, back button, haptics, Android notifications, keep-awake, F19 automation) plus `:active`/`:disabled`/`:focus` and a hard box-shadow (F23) and the `ClientSize` docs (#331). `SecureStorage` (F16) and `Speech` (F15) are merged but live in `Majorsilence.Forms.Essentials`, which is not published | **Breaking:** `OnPaint`, `ClientRectangle` and `ClientSize` are now logical units (#339), so the `ScaleTransform (e.Scaling, e.Scaling)` in every custom control drew at twice the size; removed, and `SmokeTests.CustomControls_StayInsideTheirOwnBounds_AtAnyScale` guards it | F2 (`Shared/Binding` replaced by `Majorsilence.Forms.Mvvm`) |
| 26.6.0 | 2026-10-02 | Two-way `BindText`/`BindChecked`/`BindSelectedIndex`/`BindValue` in `Majorsilence.Forms.Mvvm` (#352), the published `Majorsilence.Forms.Essentials` package (F15 `Speech`, F16 `SecureStorage`, with Android, iOS and desktop rows), ToolStrip, menu and ToolTip behaviour fixes (#351), text control fixes (#350), per-corner radius and dashed borders (F24) | None found yet | F26 (`Shared/Binding/FormBindings.cs`), F16 (`InMemorySecretStore`, now `PlatformSecretStore` over `SecureStorage`) |
| 26.7.0 | 2026-10-03 | `TextBox.InputKind` (a number pad for the PIN fields; a masked box asking for a number reports `Pin`, mapped to Digits on Android) (#368), a focused control keeps its look and `TextBox:focus` works (#366), removing a child repaints the parent (#370), the Android host disposes retired scene pictures so an animated control no longer grows native memory until the process is killed (#371), `NavigationHost`, `Form.SizeClass`, `Essentials.Launcher` and `FileSystem`, `Card` and `RichListBox`, a headless UI-thread fix | None found yet | F27 (`MemoryGuard`), F28 (the list invalidations) |

Released items: F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12, F13, F14, F15, F16, F17, F18, F19, F23, F24, F26, F27, F28

