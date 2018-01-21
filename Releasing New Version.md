# Releasing New Version

To release a new version do the following steps:

- Change the `ProductCode` for both 32 and 64bit to new GUIDs in **Product.wxs** file
- Change **Assembly Version** in `Dna.Web.Core` project properties to new version (x.x.x.y)

> **NOTE:** Updating last digit (y) won't cause old version to be removed. Update one of the first 3 digits to trigger an upgrade/remove old version

- Update **index.md** in Dna Web GitHub Pages site to point to latest download
- Update **previous.md** in Dna Web GitHub Pages site to point to older download
- Update **changelog.md** in Dna Web GitHub Pages site
- Build each installer (change configuration to Release)
- Copy .msi files to Dna Web GitHub Pages Releases folder
- Push changes to git

