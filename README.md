# REnum

> Zero-cost enums for C# in Rust-style 

## Features

- Tagged union (sum type) support via simple attributes
- Pattern matching via `.Match()` method
- Safe type access with `.IsXXX(out T)` syntax
- Supports both reference and value types
- Zero runtime overhead — everything is compile-time generated
- Generation on flight or pregeneration to exclude source generator dependency

Inspired by Rust’s `enum`, this library brings similar expressive power to C#.

## Why?

This library brings a **zero-cost abstraction** of tagged unions (enums) to C#,
similar to what Rust provides natively. Unlike runtime-based solutions like
[OneOf](https://github.com/mcintyre321/OneOf), which rely on heap allocations
and polymorphism using boxed classes, `REnum` generates **pure structs** at
compile time using **Roslyn Source Generators**.

### Benefits over alternatives like OneOf:

- **No boxing or heap allocations**: Everything is handled in value types unless you explicitly use reference types.
- **Supports static lambdas** in `.Match()` to avoid capturing context — minimizing allocations and maximizing inlining.

This makes `REnum` suitable for high-performance and low-GC environments like games and real-time systems.

## Example

```csharp
[REnum]
[REnumField(typeof(HouseAddress))]
[REnumField(typeof(ApartmentAddress))]
public partial struct Address
{
}
```

```csharp
var address = Address.FromHouseAddress(new HouseAddress {
    Street = "Rockefeller Street",
    Building = "1273"
});

if (address.IsApartmentAddress(out var apt)) {
    Console.WriteLine(apt.Street);
} else if (address.IsHouseAddress(out var house)) {
    Console.WriteLine(house.Street);
}

string label = address.Match(
    "Street is: ",
    static (prefix, house) => $"{prefix}{house.Street}",
    static (prefix, apt) => $"{prefix}{apt.Street}"
);
```

## Usage Strategies

### Import To Project

Ensure you have Roslyn Source Generator available in your root project and import both REnum.dll and REnumGenerator.dll

### Pregenerate

import REnum.dll to your project, to generate files use REnumCLI

```sh
dotnet run -- <inputDir> <outputDir>
```

## Tests

Unit-tested with NUnit and covers:

- Creation of enum instances from fields
- Comparison between enum variants
- Functional-style pattern matching via `.Match`

See `REnum.Tests` for real-world usage.

## License

MIT
