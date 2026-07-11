# Release Workflow Rules

After a batch of code changes and all usual dev cycle of build and test is completed ok. Then for the final release stage in this project, follow this release workflow:

1. Run the build script:
   `.\scripts\release.ps1 build -noninteractive`
2. If it fails, analyze and fix the compilation or test issues, then repeat step 1.
3. If it succeeds but there are "flakey tests" logged (i.e. tests that failed but then succeeded on a retry, logged in `./artifacts/flakey-tests.log`), try to fix or harden the tests to prevent future flakey tests and repeat the process from step 1.
4. Once the script succeeds with no retried/flakey tests, commit the changes to git.
5. Complete the full release process by running:
   `.\scripts\release.ps1 all -noninteractive` 
