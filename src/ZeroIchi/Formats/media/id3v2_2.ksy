meta:
  id: id3v2_2
  title: ID3v2.2 tag for .mp3 files
  file-extension: mp3
  license: MIT
  imports:
    - id3v2_3
  endian: be
seq:
  - id: tag
    type: tag
types:
  tag:
    seq:
      - id: header
        type: header
      - id: frames
        type: frame
        repeat: until
        repeat-until: _io.pos + 6 > 10 + header.size.value or _.is_invalid
        if: _io.pos + 6 <= 10 + header.size.value
      - id: padding
        size: 10 + header.size.value - _io.pos
  header:
    seq:
      - id: magic
        contents: 'ID3'
      - id: version_major
        type: u1
      - id: version_revision
        type: u1
      - id: flags
        type: flags
      - id: size
        type: id3v2_3::u4be_synchsafe
    types:
      flags:
        seq:
          - id: flag_unsynchronization
            type: b1
          - id: flag_compression
            type: b1
          - id: reserved
            type: b6
  frame:
    seq:
      - id: id
        type: str
        size: 3
        encoding: ASCII
      - id: size
        type: b24
      - id: data
        size: size
    instances:
      is_invalid:
        value: "id == '\x00\x00\x00'"
