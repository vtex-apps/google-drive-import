# Changelog

All notable changes to this project will be documented in this file.
The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed

- Revoke token if missing refresh token

## [0.5.0] - 2021-04-08

### Changed

- Use the same token for both image and catalog import

## [0.4.0] - 2021-04-05

### Changed

- Removed legacy import

## [0.3.10] - 2021-03-29

### Added

- Pagination of image files

## [0.3.9] - 2021-03-10

### Added

- Better messaging for variant matching

## [0.3.8] - 2021-03-08

### Fixed

- Fixed sku context url

## [0.3.7] - 2021-03-08

### Fixed

- Default content filter to false in case that the call to get sku details fails.

## [0.3.6] - 2021-03-05

### Changed

- Get image archive id from sku update response

## [0.3.5] - 2021-03-02

### Fixed

- Fixed skuid url

## [0.3.4] - 2021-03-01

### Changed

- Allow deactivating skus

## [0.3.3] - 2021-02-25

### Fixed
- bugfixes

## [0.3.2] - 2021-02-25

### Fixed
- bugfixes

## [0.3.1] - 2021-02-25

### Fixed
- bugfixes

## [0.3.0] - 2021-02-24

### Added
- New mapping using Google Spreadsheet
## [0.2.1] - 2021-02-11

## [0.2.0] - 2021-02-08

### Added

- Import from Google Sheets

## [0.1.3] - 2021-02-03

### Added

- Log Watch notification

## [0.1.2] - 2021-01-29

### Changed

- removed unnecessary policies 

## [0.1.1] - 2021-01-26

### Fix

- Deserialization error

## [0.1.0] - 2021-01-21

### Added

- Match sku based on Specification parameter

## [0.0.41] - 2021-01-11

### Fix
- metadata to app store

## [0.0.40] - 2021-01-07

### Fix

- If move fails, note folder in filename

## [0.0.39] - 2021-01-06

### Fix
- Fix metadata to app store

## [0.0.38] - 2020-12-22

### Added

- Create CSV list of errors

## [0.0.37] - 2020-12-21

### Changed

- Skip files that return GatewayTimeout and attempt to process on next loop

### Added

- Log error list by sku

## [0.0.36] - 2020-12-17

### Fixed	## [0.0.36] - 2020-12-17

- Fixed import lock	
- Disabled folder watch

## [0.0.35] - 2020-12-16

### Changed

- Removed import loop

## [0.0.34] - 2020-12-16

### Added

- Logging

## [0.0.33] - 2020-12-15

### Changed

- Logging

## [0.0.32] - 2020-12-15

### Changed

- Memory allocation and replica settings
- Reduced retries to one
- Added logging

## [0.0.31] - 2020-12-11

### Changed

- If upload fails because the image already exists, consider it a success.

### Fixed

- Fixed archive id lookup

## [0.0.30] - 2020-12-07

### Added

- App Icon
- Lookup image archive Id

## [0.0.29] - 2020-12-04

### Changed

- Updated Billing Options

### Added

- Logging and Error handling

## [0.0.28] - 2020-12-04

### Added

- When applying the same image to multiple skus, use archived image id

## [0.0.27] - 2020-11-26

### Changed

- Increased retry delay

## [0.0.26] - 2020-11-26

### Changed

- Create new request and client for retry

## [0.0.25] - 2020-11-25

### Added

- Retry on Timeout when adding image
- Error handling releasing lock

## [0.0.24] - 2020-11-23

### Added

- Added more logging

## [0.0.23] - 2020-11-23

### Changed

- Added parameter to set Google file page size to maximum of 1000

## [0.0.22] - 2020-09-22

### Changed

- Changed Grant Access button to official Google Signin image on admin page

## [0.0.21] - 2020-09-17

### Changed

- Corrected field descriptions on admin page

## [0.0.20] - 2020-09-16

### Changed

- Fixed bug getting product from product reference

## [0.0.19] - 2020-09-14

## [0.0.18] - 2020-09-08

### Changed

- Changed authentication server to googleauth

## [0.0.17] - 2020-08-31

### Changed

- If email not found, re-map folder ids

## [0.0.16] - 2020-08-28

### Changed

- Revoke any existing token before redirecting user when `Grant Access` is clicked on admin page

## [0.0.15] - 2020-08-28

### Added

- On Revoke Token, clear folder map

## [0.0.14] - 2020-08-28

### Added

- Retry Google requests

## [0.0.13] - 2020-08-27

### Changed

- Changed token verification logic

## [0.0.12] - 2020-08-27

### Changed

- Add ES/PT intl messages
- Admin page uses `have-token` endpoint to determine if Google account is connected

## [0.0.11] - 2020-08-27

### Changed

- Store folder Ids in VBase
- Reset Watch when signing up
- Added Error message for invalid filename

## [0.0.10] - 2020-08-26

### Changed

- Reduce calls to Google API

## [0.0.9] - 2020-08-26

### Changed

- Changed sku update request to http

## [0.0.8] - 2020-08-25

### Added

- Outbound access

## [0.0.7] - 2020-08-25

### Added

- Added logging

## [0.0.6] - 2020-08-25

### Changed

- Bugfixes

## [0.0.5] - 2020-08-20

### Changed

- Added credentials

## [0.0.4] - 2020-08-17

### Changed

- Error handling

## [0.0.3] - 2020-08-13

### Changed

- Error handling

## [0.0.2] - 2020-08-11

### Changed

- Error handling
- Bugfixes

## [0.0.1] - 2020-08-06

### Added

- Initial release
