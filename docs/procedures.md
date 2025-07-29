# Procedures

## Dev procedure (Pull request)

* Create branch to work in
* Develop
    * Write code
    * Write tests
    * Update documentation (specifications, README)
    * Commit
* Open PR (descriptive)
    * Invite people to review the changes - and fix if necessary
* Iterate: Develop - Make sure to Rebase or Merge on Main branch to keep local branch updated
* Ask for final review an approval - and fix if necessary
* Update CHANGELOG.md: Add change to "Unreleased", and commit
* Complete PR
* Delete branch (if applicable)

## Release procedure

1. Build and publish release via GitHub Actions

One built, and package about to be published:

* Update CHANGELOG.md and commit
    * Create a new section for version containing the releases previously under "Unreleased".