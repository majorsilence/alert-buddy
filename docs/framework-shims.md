# Framework shims

A shim is a temporary workaround for a framework capability that is missing or broken, allowed only to unblock progress
(PLAN.md section 11.1, policy 2). Each one is marked in code `// TEMP-SHIM (F<n>)`, listed here, and deleted in the commit that
adopts the release that fixes it. `tools/check-shims.sh` fails CI if a shim names an item that `docs/framework-versions.md`
records as released.

| Item | Where | Why | Removed when |
|---|---|---|---|
| F16 | `src/AlertBuddy.Shared/Platform/InMemorySecretStore.cs` | No desktop OS credential store exists in the framework yet, so the password/token live only for the process's lifetime | F16 releases a real desktop secret store and this repo swaps to it |

If Android delivery starts before the framework's release, expect the sound, lifecycle and back-button forwarding in the Android
head (F8, F10, F11) to need shims here too.
