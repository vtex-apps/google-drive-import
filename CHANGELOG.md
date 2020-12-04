# Changelog

All notable changes to this project will be documented in this file.
The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
