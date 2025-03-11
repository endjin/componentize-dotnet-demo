# componentize-dotnet-demo

Exploring componentize-dotnet via various examples from [James Sturtevant](https://github.com/jsturtevant) and [Eric Gregory](https://github.com/ericgregory), and exploring the use of existing .NET libraries such as [Reactive Extensions for .NET](https://github.com/dotnet/reactive) (Rx .NET) and [Ais.Net](https://github.com/ais-dotnet)

## Background Reading

- 📄 [Simplifying components for .NET/C# developers with componentize-dotnet](https://bytecodealliance.org/articles/simplifying-components-for-dotnet-developers-with-componentize-dotnet)
- 📄 [Distributing WebAssembly components using OCI registries](https://opensource.microsoft.com/blog/2024/09/25/distributing-webassembly-components-using-oci-registries/)
- 🗂️ [componentize-dotnet](https://github.com/bytecodealliance/componentize-dotnet)
- 🗂️ [csharp-components-demos](https://github.com/jsturtevant/csharp-components-demos)
- 📺 [Exploring C# Wasm Components - James Sturtevant, Microsoft](https://www.youtube.com/watch?v=cwWb-bE3c4o)
- 📺 [Contain Yourself: Wasm and the OCI Spec - Taylor Thomas, Cosmonic & James Sturtevant, Microsoft](https://www.youtube.com/watch?v=Y8ZDBOR_2Ms)

## Pre-requisites

- [Install .NET 9.0](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
- Wasmtime - `winget install BytecodeAlliance.Wasmtime`
- Install [Rust](https://www.rust-lang.org/) - `winget install Rustlang.Rustup `
- Install [cargo binstall](https://github.com/cargo-bins/cargo-binstall) - `Set-ExecutionPolicy Unrestricted -Scope Process; iex (iwr "https://raw.githubusercontent.com/cargo-bins/cargo-binstall/main/install-from-binstall-release.ps1").Content`
- Install [WebAssembly Compositions (WAC)](https://github.com/bytecodealliance/wac) CLI - `cargo binstall wac-cli`
- Install [wasm-tools](https://github.com/bytecodealliance/wasm-tools) - `cargo binstall wasm-tools`

## Hello World demo

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
```

To run `hello.wasm`:

```bash
wasmtime bin\Debug\net9.0\wasi-wasm\native\hello.wasm
```

And you should see:

```bash
Hello, World!
```

## wasi-http-server demo

Shows how reference a WebAssembly Interface Type (WIT) artifact in an OCI registry. See `wasi-http-server.csproj` for the details.

```bash
PS:> cd wasi-http-server
PS:> dotnet build
Restore complete (0.4s)
  hello succeeded (76.9s) → bin\Debug\net9.0\wasi-wasm\publish\

Build succeeded in 79.7s
```

To run `wasi-http-server.wasm` you need to specify Wasmtime server it using the CLI capability:

```bash
wasmtime serve -S cli  .\bin\Debug\net9.0\wasi-wasm\native\wasi-http-server.wasm --addr 127.0.0.1:3000 
```

In another terminal run

```bash
Invoke-RestMethod http://127.0.0.1:3000/   
```

You should see:

```bash
Hello, World!
```

## console-http-request demo

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

<details><summary><b>Show WebAssembly Interface Types (WIT) from the console WASM component.</b></summary>

```wit
package root:component;

world root {
  import wasi:cli/environment@0.2.0;
  import wasi:cli/exit@0.2.0;
  import wasi:io/error@0.2.0;
  import wasi:io/poll@0.2.0;
  import wasi:io/streams@0.2.0;
  import wasi:cli/stdin@0.2.0;
  import wasi:cli/stdout@0.2.0;
  import wasi:cli/stderr@0.2.0;
  import wasi:cli/terminal-input@0.2.0;
  import wasi:cli/terminal-output@0.2.0;
  import wasi:cli/terminal-stdin@0.2.0;
  import wasi:cli/terminal-stdout@0.2.0;
  import wasi:cli/terminal-stderr@0.2.0;
  import wasi:clocks/monotonic-clock@0.2.0;
  import wasi:clocks/wall-clock@0.2.0;
  import wasi:filesystem/types@0.2.0;
  import wasi:filesystem/preopens@0.2.0;
  import wasi:sockets/network@0.2.0;
  import wasi:sockets/udp@0.2.0;
  import wasi:sockets/tcp@0.2.0;
  import wasi:random/random@0.2.0;
  import wasi:http/types@0.2.0;
  import wasi:http/outgoing-handler@0.2.0;

  export wasi:cli/run@0.2.0;
}
package wasi:io@0.2.0 {
  interface error {
    resource error;
  }
  interface poll {
    resource pollable;

    poll: func(in: list<borrow<pollable>>) -> list<u32>;
  }
  interface streams {
    use error.{error};
    use poll.{pollable};

    resource input-stream {
      read: func(len: u64) -> result<list<u8>, stream-error>;
      blocking-read: func(len: u64) -> result<list<u8>, stream-error>;
      subscribe: func() -> pollable;
    }

    variant stream-error {
      last-operation-failed(error),
      closed,
    }

    resource output-stream {
      check-write: func() -> result<u64, stream-error>;
      write: func(contents: list<u8>) -> result<_, stream-error>;
      blocking-write-and-flush: func(contents: list<u8>) -> result<_, stream-error>;
      flush: func() -> result<_, stream-error>;
      blocking-flush: func() -> result<_, stream-error>;
      subscribe: func() -> pollable;
    }
  }
}


package wasi:cli@0.2.0 {
  interface environment {
    get-environment: func() -> list<tuple<string, string>>;

    get-arguments: func() -> list<string>;
  }
  interface exit {
    exit: func(status: result);
  }
  interface stdin {
    use wasi:io/streams@0.2.0.{input-stream};

    get-stdin: func() -> input-stream;
  }
  interface stdout {
    use wasi:io/streams@0.2.0.{output-stream};

    get-stdout: func() -> output-stream;
  }
  interface stderr {
    use wasi:io/streams@0.2.0.{output-stream};

    get-stderr: func() -> output-stream;
  }
  interface terminal-input {
    resource terminal-input;
  }
  interface terminal-output {
    resource terminal-output;
  }
  interface terminal-stdin {
    use terminal-input.{terminal-input};

    get-terminal-stdin: func() -> option<terminal-input>;
  }
  interface terminal-stdout {
    use terminal-output.{terminal-output};

    get-terminal-stdout: func() -> option<terminal-output>;
  }
  interface terminal-stderr {
    use terminal-output.{terminal-output};

    get-terminal-stderr: func() -> option<terminal-output>;
  }
  interface run {
    run: func() -> result;
  }
}


package wasi:clocks@0.2.0 {
  interface monotonic-clock {
    use wasi:io/poll@0.2.0.{pollable};

    type duration = u64;

    type instant = u64;

    now: func() -> instant;

    subscribe-instant: func(when: instant) -> pollable;

    subscribe-duration: func(when: duration) -> pollable;
  }
  interface wall-clock {
    record datetime {
      seconds: u64,
      nanoseconds: u32,
    }

    now: func() -> datetime;
  }
}


package wasi:filesystem@0.2.0 {
  interface types {
    use wasi:io/streams@0.2.0.{input-stream, output-stream};
    use wasi:clocks/wall-clock@0.2.0.{datetime};
    use wasi:io/streams@0.2.0.{error};

    resource descriptor {
      read-via-stream: func(offset: filesize) -> result<input-stream, error-code>;
      write-via-stream: func(offset: filesize) -> result<output-stream, error-code>;
      append-via-stream: func() -> result<output-stream, error-code>;
      advise: func(offset: filesize, length: filesize, advice: advice) -> result<_, error-code>;
      get-flags: func() -> result<descriptor-flags, error-code>;
      get-type: func() -> result<descriptor-type, error-code>;
      set-size: func(size: filesize) -> result<_, error-code>;
      read: func(length: filesize, offset: filesize) -> result<tuple<list<u8>, bool>, error-code>;
      read-directory: func() -> result<directory-entry-stream, error-code>;
      stat: func() -> result<descriptor-stat, error-code>;
      stat-at: func(path-flags: path-flags, path: string) -> result<descriptor-stat, error-code>;
      open-at: func(path-flags: path-flags, path: string, open-flags: open-flags, %flags: descriptor-flags) -> result<descriptor, error-code>;
      readlink-at: func(path: string) -> result<string, error-code>;
      unlink-file-at: func(path: string) -> result<_, error-code>;
      metadata-hash: func() -> result<metadata-hash-value, error-code>;
      metadata-hash-at: func(path-flags: path-flags, path: string) -> result<metadata-hash-value, error-code>;
    }

    type filesize = u64;

    enum error-code {
      access,
      would-block,
      already,
      bad-descriptor,
      busy,
      deadlock,
      quota,
      exist,
      file-too-large,
      illegal-byte-sequence,
      in-progress,
      interrupted,
      invalid,
      io,
      is-directory,
      loop,
      too-many-links,
      message-size,
      name-too-long,
      no-device,
      no-entry,
      no-lock,
      insufficient-memory,
      insufficient-space,
      not-directory,
      not-empty,
      not-recoverable,
      unsupported,
      no-tty,
      no-such-device,
      overflow,
      not-permitted,
      pipe,
      read-only,
      invalid-seek,
      text-file-busy,
      cross-device,
    }

    enum advice {
      normal,
      sequential,
      random,
      will-need,
      dont-need,
      no-reuse,
    }

    flags descriptor-flags {
      read,
      write,
      file-integrity-sync,
      data-integrity-sync,
      requested-write-sync,
      mutate-directory,
    }

    enum descriptor-type {
      unknown,
      block-device,
      character-device,
      directory,
      fifo,
      symbolic-link,
      regular-file,
      socket,
    }

    resource directory-entry-stream {
      read-directory-entry: func() -> result<option<directory-entry>, error-code>;
    }

    type link-count = u64;

    record descriptor-stat {
      %type: descriptor-type,
      link-count: link-count,
      size: filesize,
      data-access-timestamp: option<datetime>,
      data-modification-timestamp: option<datetime>,
      status-change-timestamp: option<datetime>,
    }

    flags path-flags {
      symlink-follow,
    }

    flags open-flags {
      create,
      directory,
      exclusive,
      truncate,
    }

    record metadata-hash-value {
      lower: u64,
      upper: u64,
    }

    record directory-entry {
      %type: descriptor-type,
      name: string,
    }

    filesystem-error-code: func(err: borrow<error>) -> option<error-code>;
  }
  interface preopens {
    use types.{descriptor};

    get-directories: func() -> list<tuple<descriptor, string>>;
  }
}


package wasi:sockets@0.2.0 {
  interface network {
    enum error-code {
      unknown,
      access-denied,
      not-supported,
      invalid-argument,
      out-of-memory,
      timeout,
      concurrency-conflict,
      not-in-progress,
      would-block,
      invalid-state,
      new-socket-limit,
      address-not-bindable,
      address-in-use,
      remote-unreachable,
      connection-refused,
      connection-reset,
      connection-aborted,
      datagram-too-large,
      name-unresolvable,
      temporary-resolver-failure,
      permanent-resolver-failure,
    }
  }
  interface udp {
    resource udp-socket;

    resource incoming-datagram-stream;

    resource outgoing-datagram-stream;
  }
  interface tcp {
    use wasi:io/streams@0.2.0.{input-stream, output-stream};
    use network.{error-code};

    resource tcp-socket {
      finish-connect: func() -> result<tuple<input-stream, output-stream>, error-code>;
    }
  }
}


package wasi:random@0.2.0 {
  interface random {
    get-random-bytes: func(len: u64) -> list<u8>;
  }
}


package wasi:http@0.2.0 {
  interface types {
    use wasi:io/streams@0.2.0.{input-stream, output-stream};
    use wasi:io/poll@0.2.0.{pollable};

    resource fields {
      from-list: static func(entries: list<tuple<field-key, field-value>>) -> result<fields, header-error>;
      entries: func() -> list<tuple<field-key, field-value>>;
    }

    type field-key = string;

    type field-value = list<u8>;

    variant header-error {
      invalid-syntax,
      forbidden,
      immutable,
    }

    type headers = fields;

    resource outgoing-request {
      constructor(headers: headers);
      body: func() -> result<outgoing-body>;
      set-method: func(method: method) -> result;
      set-path-with-query: func(path-with-query: option<string>) -> result;
      set-scheme: func(scheme: option<scheme>) -> result;
      set-authority: func(authority: option<string>) -> result;
    }

    resource outgoing-body {
      write: func() -> result<output-stream>;
      finish: static func(this: outgoing-body, trailers: option<trailers>) -> result<_, error-code>;
    }

    variant method {
      get,
      head,
      post,
      put,
      delete,
      connect,
      options,
      trace,
      patch,
      other(string),
    }

    variant scheme {
      HTTP,
      HTTPS,
      other(string),
    }

    resource incoming-response {
      status: func() -> status-code;
      headers: func() -> headers;
      consume: func() -> result<incoming-body>;
    }

    type status-code = u16;

    resource incoming-body {
      %stream: func() -> result<input-stream>;
      finish: static func(this: incoming-body) -> future-trailers;
    }

    resource future-trailers;

    type trailers = fields;

    record DNS-error-payload {
      rcode: option<string>,
      info-code: option<u16>,
    }

    record TLS-alert-received-payload {
      alert-id: option<u8>,
      alert-message: option<string>,
    }

    record field-size-payload {
      field-name: option<string>,
      field-size: option<u32>,
    }

    variant error-code {
      DNS-timeout,
      DNS-error(DNS-error-payload),
      destination-not-found,
      destination-unavailable,
      destination-IP-prohibited,
      destination-IP-unroutable,
      connection-refused,
      connection-terminated,
      connection-timeout,
      connection-read-timeout,
      connection-write-timeout,
      connection-limit-reached,
      TLS-protocol-error,
      TLS-certificate-error,
      TLS-alert-received(TLS-alert-received-payload),
      HTTP-request-denied,
      HTTP-request-length-required,
      HTTP-request-body-size(option<u64>),
      HTTP-request-method-invalid,
      HTTP-request-URI-invalid,
      HTTP-request-URI-too-long,
      HTTP-request-header-section-size(option<u32>),
      HTTP-request-header-size(option<field-size-payload>),
      HTTP-request-trailer-section-size(option<u32>),
      HTTP-request-trailer-size(field-size-payload),
      HTTP-response-incomplete,
      HTTP-response-header-size(field-size-payload),
      HTTP-response-body-size(option<u64>),
      HTTP-response-trailer-section-size(option<u32>),
      HTTP-response-trailer-size(field-size-payload),
      HTTP-response-transfer-coding(option<string>),
      HTTP-response-content-coding(option<string>),
      HTTP-response-timeout,
      HTTP-upgrade-failed,
      HTTP-protocol-error,
      loop-detected,
      configuration-error,
      internal-error(option<string>),
    }

    resource future-incoming-response {
      subscribe: func() -> pollable;
      get: func() -> option<result<result<incoming-response, error-code>>>;
    }

    resource request-options;
  }
  interface outgoing-handler {
    use types.{outgoing-request, request-options, future-incoming-response, error-code};

    handle: func(request: outgoing-request, options: option<request-options>) -> result<future-incoming-response, error-code>;
  }
}
```
</details>

To run `console.wasm` you need to specify Wasmtime to use the the HTTP Interface when running the module:

```bash
wasmtime run -S http .\dist\console.wasm
```

## console-http-request-rx demo

Shows how to use Reactive Extensions for .NET to several make an HTTP requests while running in a WASI environment.

```bash
PS:> cd console-http-request-rx
PS:> dotnet build
Restore complete (0.4s)
  console succeeded (3.5s) → bin\Debug\net9.0\wasi-wasm\publish\

Build succeeded in 6.3s
```

To run `console.wasm`, you need to specify Wasmtime to use the the HTTP Interface when running the module:

```bash
wasmtime run -S http .\dist\console.wasm
```

## wasi-sockets demo

Shows how to make a HTTP request using WASI sockets module.

```bash
PS:> cd .\wasi-sockets\
PS:> dotnet build
Restore complete (0.4s)
  wasi-sockets succeeded (2.7s) → bin\Debug\net9.0\wasi-wasm\publish\

Build succeeded in 4.6s
```

To run the `wasi-sockets.wasm`, you need to use Wasmtime's security features to enable the required network capabilities:

```bash
wasmtime -S inherit-network=y .\dist\wasi-sockets.wasm
```

## Ais.Net.Receiver.Host.Wasi demo

This demo showcases how to take an existing, mature .NET Application, and providing an alternative, custom WASI based implementation of the TCP connectivity subsystem, so the application can run as a WASM app with working TCP connectivity. 

We create `WasiSocketNmeaStreamReader`, a WASI-based implementation of `INmeaStreamReader` that closely mimics the .NET based `TcpClientNmeaStreamReader` from the [Ais.Net.Receiver](https://github.com/ais-dotnet/Ais.Net.Receiver) library. It uses the WASI sockets API to connect to the Norwegian Coastal Administration's TCP endpoint to receive maritime vessel tracking data, and processes raw AIS AIVDM/AIVDO sentences (standardized maritime vessel tracking messages).

Technical Benefits:

- Allows AIS data (GPS for Marine Vessels) processing in WebAssembly environments
- Demonstrates how network-dependent .NET libraries can be adapted for WASI
- Shows practical use of the WASI sockets API with real-world data sources
- The data stream covers vessel movements approximately 40-60 nautical miles from the Norwegian coastline, collected from over 50 coastal base stations.

The Data ingested by the AIS.Net.Receiver is licensed under the [Norwegian license for public data (NLOD)](https://data.norge.no/nlod/en/2.0)

```bash
PS:> cd .\Ais.Net.Receiver.Host.Wasi\
PS:>dotnet build
Restore complete (1.0s)
  Ais.Net.Receiver.Host.Wasi succeeded (9.2s) → bin\Debug\net9.0\wasi-wasm\publish\

Build succeeded in 16.0s
```

To run `Ais.Net.Receiver.Host.Wasi.wasm` you need to use Wasmtime's security features to enable the required network capabilities:

```bash
wasmtime -S inherit-network=y .\dist\Ais.Net.Receiver.Host.Wasi.wasm
```

### Run on a Raspberry Pi

Copy the output to your Pi:

```bash
scp .\dist\Ais.Net.Receiver.Host.Wasi.wasm pi@pi:~\   
```

SSH into your Pi, and install Wasmtime:

```bash
curl https://wasmtime.dev/install.sh -sSf | bash
```

Run the WASM module:

```bash
wasmtime -S inherit-network=y Ais.Net.Receiver.Host.Wasi.wasm
```