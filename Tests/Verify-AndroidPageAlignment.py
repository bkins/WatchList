"""Check every ELF PT_LOAD alignment in an APK, without executing or installing it."""

import argparse
import json
import struct
import zipfile


def read_load_alignments(data):
    if data[:4] != b"\x7fELF":
        raise ValueError("Native library is not an ELF binary")
    byte_order = "<" if data[5] == 1 else ">"
    if data[4] == 2:
        table_offset = struct.unpack_from(byte_order + "Q", data, 32)[0]
        entry_size, entry_count = struct.unpack_from(byte_order + "HH", data, 54)
        alignment_offset, alignment_type = 48, "Q"
    elif data[4] == 1:
        table_offset = struct.unpack_from(byte_order + "I", data, 28)[0]
        entry_size, entry_count = struct.unpack_from(byte_order + "HH", data, 42)
        alignment_offset, alignment_type = 28, "I"
    else:
        raise ValueError("Unsupported ELF class")

    alignments = []
    for index in range(entry_count):
        offset = table_offset + index * entry_size
        segment_type = struct.unpack_from(byte_order + "I", data, offset)[0]
        if segment_type == 1:
            alignments.append(struct.unpack_from(byte_order + alignment_type, data, offset + alignment_offset)[0])
    if not alignments:
        raise ValueError("ELF has no loadable segments")
    return alignments


def verify_apk(apk_path):
    results = {}
    with zipfile.ZipFile(apk_path) as archive:
        for name in archive.namelist():
            if name.startswith("lib/") and name.endswith(".so"):
                alignments = read_load_alignments(archive.read(name))
                if min(alignments) < 16384:
                    raise ValueError(f"{name}: load alignment is smaller than 16 KB: {alignments}")
                results[name] = alignments
    if not results:
        raise ValueError("APK contains no native libraries")
    return results


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("apk")
    arguments = parser.parse_args()
    print(json.dumps({"result": "PASS", "load_alignments": verify_apk(arguments.apk)}, indent=2))
