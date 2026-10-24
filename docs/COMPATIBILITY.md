# Verified compatibility and transport

Verified on one Lenovo IdeaPad Z500, product 20202, BIOS 71CN51WW (V1.21), with a third-party replacement pack. Windows direct pack/cell reads work; Windows calibration commands remain untested.

| Component | Required inspected value |
| --- | --- |
| DSDT SHA-256 | `3aa6787391cf1e44a0632e5c63f2ead524fcbcecf88cd49aef5de498d3db735f` |
| Lenovo AcpiVpc.sys version | `15.11.29.70` |
| Driver SHA-256 | `6bc61af7806a6482754af7757ae7dd013d345bcc92e18901f6474fefc3e07c8d` |

Both fingerprint checks run before a driver request. Different firmware/driver versions are deliberately unsupported until inspected. No bypass switch is provided.

## Windows path

`Z500Battery.exe -> \\.\EnergyDrv -> AcpiVpc.sys -> Lenovo VPC0 ACPI methods -> EC SMBus -> battery`

The installed driver signature validated through Windows as Microsoft Windows Hardware Compatibility Publisher on the reference machine. No driver binary is redistributed. Administrator access is required by the driver's token check for MHPF.

| Operation | Driver request | Firmware method |
| --- | --- | --- |
| Battery mode read | `0x831020F8`, first input byte `FF`, four-byte result | `GBMD` |
| Battery read transaction | `0x83102114`, 37-byte request/response | `MHPF` |
| Experimental calibration | `0x831020F8`, first byte `1` / `0`; only custom-enabled builds | `SBMC` |

Firmware path: `\_SB.PCI0.LPCB.EC0.VPC0`. MHPF uses protocol `09` for read-word or `0B` for read-block; shifted battery address `16`; register command at byte 3; payload starts at byte 4. Successful observed status is `80`. The app validates response size, echoed fields and status. There are no arbitrary ACPI methods or battery configuration writes.

The bridge does not provide a trustworthy returned block length. Raw blocks show the 32-byte response area, including possibly stale trailing data. Text is shown only as a reported prefix; chemistry uses the established LION prefix. The supported pack's block `23` begins `42 01 01 01`; payload bytes 4–11 hold four little-endian mV words. Cell decoding checks that header, plausibility bounds and agreement with pack voltage. Bounds are validation heuristics, not safe-discharge thresholds. Unknown trailing bytes remain raw.

GBMD bit 0 reports calibration mode, not successful learning. Other firmware flag names are retained as symbols and must not be expanded into fault diagnoses without evidence.

## Reporting another configuration

Report model, BIOS version, Windows version, Lenovo driver version and whether fingerprint checks pass. Do not upload full battery snapshots without redacting serial/identity fields. A different pack may use a different manufacturer-data layout even on matching firmware. Do not send guessed manufacturer commands or remove guards to test compatibility.
