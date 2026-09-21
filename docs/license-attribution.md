# License and attribution

This project is MIT licensed.

## Research references

- `trafficpest/fcbtool`, copyright 2024 Jason March, MIT License. Its public source and V-AMP dump
  were studied; the dump is retained as a hex fixture with provenance metadata.
- `riban-bw/fcb1010`, copyright 2021 Brian Walton, MIT License. Its reverse-engineered field order,
  global offsets and stock-v2.5 caveat were used as protocol evidence.

No source files were copied into the implementation. The codec is an independent C# implementation
with stricter validation, typed models, lossless raw preservation and separate transport/UI layers.
The required upstream copyright and permission notices are reproduced in `ThirdPartyNotices.txt`.

Public manuals were used as documentation/evidence only. No source, resource, or binary from the
proprietary FCB/UnO ControlCenter was copied or reverse engineered.
