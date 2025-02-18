# componentize-dotnet-demo
Exploring componentize-dotnet

## Pre-requisites

- [Install .NET 9.0](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
- Wasmtime - `winget install BytecodeAlliance.Wasmtime`
- Install [Rust](https://www.rust-lang.org/) - `winget install Rustlang.Rustup `
- Install [cargo binstall](https://github.com/cargo-bins/cargo-binstall) - `Set-ExecutionPolicy Unrestricted -Scope Process; iex (iwr "https://raw.githubusercontent.com/cargo-bins/cargo-binstall/main/install-from-binstall-release.ps1").Content`
- Install [WebAssembly Compositions (WAC)](https://github.com/bytecodealliance/wac) CLI - `cargo binstall wac-cli`
- Install [wasm-tools](https://github.com/bytecodealliance/wasm-tools) - `cargo binstall wasm-tools`

## Hello World Demo

Shows how to compile a simple hello world application to WASM

```bash
PS:> cd hello-world
PS:> dotnet restore
Restore complete (29.5s)

Build succeeded in 29.6s
PS:> dotnet build
Restore complete (0.4s)
  hello succeeded (76.9s) → bin\Debug\net9.0\wasi-wasm\publish\

Build succeeded in 79.7s

PS:> wasmtime bin\Debug\net9.0\wasi-wasm\native\hello.wasm
Hello, World!
```

## wasi-http-server Demo

Shows how reference a WebAssembly Interface Type (WIT) artifact in an OCI registry. See `wasi-http-server.csproj` for the details.

```bash
PS:> cd wasi-http
PS:> dotnet build
Restore complete (0.4s)
  hello succeeded (76.9s) → bin\Debug\net9.0\wasi-wasm\publish\

Build succeeded in 79.7s

PS:> wasmtime serve -S cli  .\bin\Debug\net9.0\wasi-wasm\native\wasi-http.wasm --addr 127.0.0.1:3000 
```

In another terminal

```bash
PS:> Invoke-RestMethod http://127.0.0.1:3000/   
Hello, World!
```

## console-http-request Demo

Shows how to make an HTTP request while running in a WASI environment.

```bash
PS:> cd console-http-request
PS:> dotnet build
Restore complete (0.4s)
  console succeeded (3.5s) → bin\Debug\net9.0\wasi-wasm\publish\

Build succeeded in 6.3s
```

To see the WIT Components in the WASM file, run:

```bash
wasm-tools component wit .\dist\console.wasm

```

To run, the console, you need to specify Wasmtime to use the the HTTP Interface when running the module:

```bash
wasmtime run -S http .\dist\console.wasm
```