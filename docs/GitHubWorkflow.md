# Deploying Updates from the `work` Branch

Use the steps below to publish the current `work` branch to your GitHub repository and download a ready-to-upload archive if you prefer to skip Git entirely.

## Push directly to GitHub
1. Confirm the branch and clean working tree:
   ```bash
   git status -sb
   ```
2. Add your GitHub repository as a remote (run once per clone, replace placeholders with your data):
   ```bash
   git remote add origin https://github.com/<your-account>/<repo>.git
   ```
3. Push the `work` branch to `main` on GitHub:
   ```bash
   git push -u origin work:main
   ```
4. Open the repository page on GitHub, switch to the **main** branch, and you will see this commit in the history. Use the green **Code** button to copy the HTTPS/SSH URL or download a ZIP of the branch.

## Export an archive locally
If you would rather upload files manually, create a ZIP from the current codebase:
```bash
git archive --format=zip HEAD -o TheAddiction-latest.zip
```
The archive contains the same files that will be pushed to GitHub. You can upload this ZIP directly to your server.
