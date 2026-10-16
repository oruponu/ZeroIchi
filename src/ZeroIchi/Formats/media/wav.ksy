meta:
  id: wav
  title: Waveform Audio File Format
  file-extension: wav
  -zeroichi-magic: 52 49 46 46 ?? ?? ?? ?? 57 41 56 45
  license: MIT
  endian: le
seq:
  - id: magic
    contents: RIFF
  - id: len_data
    type: u4
  - id: form_type
    contents: WAVE
  - id: chunks
    type: chunk
    repeat: eos
types:
  chunk:
    -webide-representation: '{id}'
    seq:
      - id: id
        type: str
        size: 4
        encoding: ASCII
      - id: len_data
        type: u4
      - id: data
        size: len_data
        type:
          switch-on: id
          cases:
            '"fmt "': fmt_chunk
            '"LIST"': list_chunk
      - id: padding
        size: 1
        if: len_data % 2 != 0 and not _io.eof
  fmt_chunk:
    seq:
      - id: format_tag
        type: u2
        enum: format_tags
      - id: num_channels
        type: u2
      - id: sample_rate
        type: u4
      - id: avg_bytes_per_sec
        type: u4
      - id: block_align
        type: u2
      - id: bits_per_sample
        type: u2
        if: not _io.eof
      - id: len_extra
        type: u2
        if: not _io.eof
      - id: extra
        size-eos: true
        if: not _io.eof
  list_chunk:
    seq:
      - id: list_type
        type: str
        size: 4
        encoding: ASCII
      - id: items
        type: info_item
        repeat: eos
        if: list_type == "INFO"
      - id: data
        size-eos: true
        if: list_type != "INFO"
  info_item:
    -webide-representation: '{id}'
    seq:
      - id: id
        type: str
        size: 4
        encoding: ASCII
      - id: len_data
        type: u4
      - id: value
        type: str
        size: len_data
        terminator: 0
        encoding: UTF-8
      - id: padding
        size: 1
        if: len_data % 2 != 0 and not _io.eof
enums:
  format_tags:
    0x0001: pcm
    0x0002: adpcm
    0x0003: ieee_float
    0x0006: alaw
    0x0007: mulaw
    0xfffe: extensible
