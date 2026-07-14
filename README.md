# RabiRiichi (兔兔立直)

[![100hun](https://100hun.rabimimi.com/provider/codecov/github/RabiMimi/RabiRiichi/develop/badge.png?size=128)](https://github.com/KCFindstr/100hun)

[![build-test](https://github.com/RabiMimi/RabiRiichi/actions/workflows/build-test.yml/badge.svg)](https://github.com/RabiMimi/RabiRiichi/actions/workflows/build-test.yml)
[![deploy](https://github.com/RabiMimi/RabiRiichi/actions/workflows/deploy.yml/badge.svg)](https://github.com/RabiMimi/RabiRiichi/actions/workflows/deploy.yml)
[![deploy-dev](https://github.com/RabiMimi/RabiRiichi/actions/workflows/deploy-dev.yml/badge.svg)](https://github.com/RabiMimi/RabiRiichi/actions/workflows/deploy-dev.yml)
[![codecov](https://codecov.io/gh/RabiMimi/RabiRiichi/branch/develop/graph/badge.svg?token=MKLFTP3O4C)](https://codecov.io/gh/RabiMimi/RabiRiichi)
[![License: AGPL v3](https://img.shields.io/badge/License-AGPL_v3-blue.svg)](https://www.gnu.org/licenses/agpl-3.0)
![Code Size](https://img.shields.io/github/languages/code-size/RabiMimi/RabiRiichi)

RabiRiichi is a riichi mahjong module for .NET Core.

See [Documentation](https://riichi-docs.rabimimi.com) for more information. (Currently only available in Simplified Chinese)

## Hosted Servers

| Environment | Address                                                    |
| ----------- | ---------------------------------------------------------- |
| Production  | [RabiRiichi 兔兔立直](https://riichi.rabimimi.com)         |
| Development | [RabiRiichi-dev 兔兔开发](https://riichi-dev.rabimimi.com) |

## Development

To develop, clone this repository and open `RabiRiichi.sln`. [.NET 9 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0) is required.

This repository uses git submodule to manage shared proto files and gRPC services. To init git submodule, run:

```bash
$ git submodule update --init --remote
```
