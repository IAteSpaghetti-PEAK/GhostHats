# Releasing

The release is automated, but it has an anonymity step that is not. Do them in order.

## Identity

This repo commits as `Developer <developer@localhost>`, set in its **local** git config, so check
`git config --local user.name` and `user.email`. There is no global identity on this machine, so a
fresh clone commits as nobody until it's set again. Check before your first commit in a new clone:

```bash
git config --local user.name && git config --local user.email
```

No `Co-Authored-By` trailers in this repo's commits, ever. They carry an email address.

## Cutting a release

1. Bump `version_number` in `thunderstore/manifest.json`. It is the single source of truth, so the
   csproj `<Version>`, the `PluginVersion` const in `src/Plugin.cs`, and the changelog heading
   should all match it.
2. Add the version's entry to `thunderstore/CHANGELOG.md`. The workflow uses this file verbatim as
   the GitHub release notes.
3. Run the packager. It writes `artifacts/GhostHats-<version>.zip` and refreshes
   `release-assets/` with the zip and DLL the release will attach:

   ```bash
   powershell -ExecutionPolicy Bypass -File .\package-thunderstore.ps1
   ```

4. Commit everything, including `release-assets/`. The GitHub runner can't build the mod, because
   that needs PEAK's own DLLs and those aren't public, so the binaries have to be committed.
5. Tag and push:

   ```bash
   git tag v<version> && git push origin main --tags
   ```

6. **Delete the workflow run.** The release is authored by `github-actions[bot]`, but the account
   that pushed the tag shows up as the run's actor, which links a real profile to the repo. Once
   the run has finished, delete it:

   ```bash
   gh api -X DELETE /repos/IAteSpaghetti-PEAK/GhostHats/actions/runs/<run-id>
   ```

   Find the id with `gh run list --repo IAteSpaghetti-PEAK/GhostHats`. Deleting the run does not
   affect the release or the Thunderstore upload. Both are already done by then.

## Thunderstore

Publishing happens in the workflow only if the `THUNDERSTORE_API_TOKEN` repo secret is set.
Without it the job logs a notice and exits clean, and you upload
`artifacts/GhostHats-<version>.zip` by hand at <https://thunderstore.io/c/peak/create/>.

The package namespace is `IAteSpaghetti` (`thunderstore.toml`), matching the other PEAK mods.
