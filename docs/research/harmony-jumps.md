# Harmony's jumps under Proton

Notes on a crash that showed up after a few hot reloads, and why ObenLoader now forces Harmony to write its jumps inline (`src/ObenLoader/Runtime/InlineJumps.cs`).

## What we saw

After a handful of cascade reloads (ObenFramework and everything that builds on it), patched game methods started throwing `NullReferenceException` on their very first instruction:

```
NullReferenceException: Object reference not set to an instance of an object
  at DeveloperConsole.Print (System.String text) <0x7b951740 + 0x00000>
```

`+ 0x00000` with no IL offset means the fault is on the method's first byte, which is where Harmony's jump lives. Shortly after, the game crashed natively in `(wrapper delegate-invoke) invoke_void_TimeAndDay` from `TimeOfDayAzure.UpdateCurrentTimeAndDay`, the same thing reached through a delegate.

## How a patched method jumps

Harmony (through the MonoMod.Core copy bundled in `0Harmony.dll`) overwrites the start of the original method with a jump to the generated replacement. On x86-64 it picks one of three forms:

| Kind | Bytes | When |
| --- | --- | --- |
| rel32 | `E9 xx xx xx xx` | replacement within 2 GB of the method |
| rel32 indirect | `FF 25 disp32`, target in an 8-byte cell elsewhere | replacement further away and MonoMod could allocate a cell within 2 GB |
| absolute | `FF 25 00 00 00 00` + 8-byte target | otherwise; the target sits right after the instruction, inside the method |

For the indirect kind, MonoMod finds a free spot near the method with `VirtualQuery` and allocates a 4 KB page there with `VirtualAlloc` (`QueryingPagedMemoryAllocator`).

On a fresh start every replacement is close enough for rel32. After two or three reloads Mono puts new code further out, and most re-applied patches switch to the indirect kind.

## What went wrong

Reading the first bytes of the broken methods and asking Windows (Wine) about the cell addresses showed that the cells were no longer ours. Right after the cascade that created them, each cell address was `MEM_RESERVE` inside one 512 MB reservation based at `0x213a0000` that belongs to some other allocator, and MonoMod's 4 KB pages no longer existed as allocations. MonoMod's own page cleanup wasn't the cause: blocking it changed nothing. With the cell gone, the jump reads unmapped memory (an NRE at offset 0), or a cell from a page mapped later at the same address (a jump into the wrong code).

We didn't chase down which component's reservation swallows the pages under Wine. The fix doesn't depend on it.

## The fix

ObenLoader patches `PagedMemoryAllocator.TryAllocateInRange` before any mod loads and refuses non-executable requests, which are exactly the jump cells. MonoMod then falls back to the absolute form, which keeps the target inside the method's own code. Executable requests (MonoMod's alt-entry stubs, unused by Harmony patches) still go through.

Stress test on 2026-10-28: 12 cascade reloads with a forced full GC after each. Before the fix, 68 of 86 indirect jumps were dead after the third cascade and the game crashed on the fifth. After it, there were 0 indirect jumps, up to 125 absolute ones, 0 broken and no NREs.

## Checking it

`oben call patches.check` lists every patched method's jump kind and reports any whose cell or target memory isn't committed any more (`all=true` lists every method). Run it after a long hot-reload session if something patched starts misbehaving.
