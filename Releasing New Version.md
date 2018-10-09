# Releasing New Version

To release a new version do the following steps:

- Change the `ProductCode` for both 32 and 64bit to new GUIDs in **Product.wxs** file
- Change **Assembly Version** in `Dna.Web.Core` project properties to new version (x.x.x.y)

> **NOTE:** Updating last digit (y) won't cause old version to be removed. Update one of the first 3 digits to trigger an upgrade/remove old version

- Update **changelog.dhtml** and **Releases.json** in DnaWeb website 
- Build each installer (change configuration to Release)
- Zip and copy installer files to DnaWeb.io Releases folder
- Push changes to git
- Push new website live

