# frikadellen-fancy

[![GitHub issues](https://img.shields.io/github/issues/skriptkiddielmaoxd/frikadellen-fancy)](https://github.com/skriptkiddielmaoxd/frikadellen-fancy/issues)
[![GitHub stars](https://img.shields.io/github/stars/skriptkiddielmaoxd/frikadellen-fancy)](https://github.com/skriptkiddielmaoxd/frikadellen-fancy/stargazers)
[![License](https://img.shields.io/github/license/skriptkiddielmaoxd/frikadellen-fancy)](./LICENSE)

## Overview

**frikadellen-fancy** is an extended version of [frikadellen-baf-121](https://github.com/TreXito/frikadellen-baf-121) by [@TreXito](https://github.com/TreXito), adding a modern, cross-platform interface and improved integration capabilities.

- **Cross-platform Powered by Avalonia:** The UI is built using [Avalonia](https://avaloniaui.net/), enabling native performance on **Windows, Linux, macOS**, and more.
- **Standalone Compatible:** The original core application (written in Rust) still functions as a standalone app. You can use it independently or via this enhanced interface.
- **C# Interface:** Provides a robust .NET/C# API for integration into new or existing C# projects.
- **Parallel Development:** Tracks changes and features in the [upstream repo](https://github.com/TreXito/frikadellen-baf-121) for close compatibility.

## Features

- 🖥️ **Avalonia UI:** True cross-platform support—run the same UI everywhere.
- 🤝 **Rust Backend:** Leverages the power and reliability of the original Rust app.
- 💻 **C#/.NET Compatibility:** Integrate the backend from your .NET applications or via the Avalonia UI.
- 🔗 **Interoperability:** C# layer bridges the original logic and modern desktop environments.
- 🛠️ **Open Collaboration:** Contributions, feedback, and suggestions are always welcome!

## Getting Started

### Prerequisites

- [.NET SDK (6+ recommended)](https://dotnet.microsoft.com/download)
- [Rust (for standalone/backend use)](https://www.rust-lang.org/)
- Basic understanding of C# and/or Rust

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/skriptkiddielmaoxd/frikadellen-fancy.git
   cd frikadellen-fancy
   ```

2. **Build the solution**
   ```bash
   dotnet build
   ```

3. **Run the Avalonia App**
   ```bash
   dotnet run --project src/Frikadellen.Fancy.Interface
   ```

4. *(Optional)* **Build and use the original Rust backend**  
   For instructions, see [frikadellen-baf-121](https://github.com/TreXito/frikadellen-baf-121).

### Usage Example

```csharp
using Frikadellen.Fancy.Interface;

// Create and use the main interface
var frikadellen = new FancyFrikadellen();
// Call your platform-independent methods
frikadellen.DoSomethingFancy();
```

*Explore the [Avalonia](https://avaloniaui.net/) docs for theming or platform customization!*

## Project Structure

- `/src` — C# interface and Avalonia UI
- `/backend` — (Optional) Rust backend logic
- `/tests` — Unit and integration tests
- `/docs` — Documentation and design notes

## Compatibility

- **Avalonia-powered UI**: Works on Windows, Linux, macOS, and other platforms supported by Avalonia.
- **Headless/Backend**: Frikadellen-baf-121 (Rust) still works as a standalone app.
- **Interfacing**: Use the C# layer or interact directly with the backend per your requirements.

## Community & Support

- **Discord Server:** [frikadellenBAF on Discord](https://discord.gg/bxqXBefY)  
- **Contact:** skriptkiddielmaoxd (@_standonit_ on Discord)

Join the conversation, get support, or share your feedback—everyone is welcome!

## License

This project is licensed under the terms of the [LICENSE](./LICENSE) file.

---

**Related Projects**
- [frikadellen-baf-121](https://github.com/TreXito/frikadellen-baf-121) — The upstream Rust core

*Many thanks to @TreXito for original inspiration and ongoing collaboration.*
