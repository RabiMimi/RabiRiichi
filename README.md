# RabiRiichi (拉比立直)

[![build-test](https://github.com/RabiMimi/RabiRiichi/actions/workflows/build-test.yml/badge.svg)](https://github.com/RabiMimi/RabiRiichi/actions)
[![codecov](https://codecov.io/gh/RabiMimi/RabiRiichi/branch/develop/graph/badge.svg?token=MKLFTP3O4C)](https://codecov.io/gh/RabiMimi/RabiRiichi)
[![License: AGPL v3](https://img.shields.io/badge/License-AGPL_v3-blue.svg)](https://www.gnu.org/licenses/agpl-3.0)
![Code Size](https://img.shields.io/github/languages/code-size/RabiMimi/RabiRiichi)

RabiRiichi is a riichi mahjong module for .NET Core.

See [Documentation](https://riichi-docs.rabimimi.com) for more information. (Currently only available in Simplified Chinese)

## Development

To develop, clone this repository and open `RabiRiichi.sln`. [.NET 6 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/6.0) is required.

This repository uses git submodule to manage shared proto files and gRPC services. To init git submodule, run:

```bash
$ git submodule update --init --remote
```
