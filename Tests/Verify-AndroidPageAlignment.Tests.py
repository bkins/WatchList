"""Regression tests for the APK verifier, including rejection of unsafe/incomplete inputs."""

import importlib.util
import pathlib
import struct
import tempfile
import unittest
import zipfile

module_path = pathlib.Path(__file__).with_name("Verify-AndroidPageAlignment.py")
specification = importlib.util.spec_from_file_location("android_alignment", module_path)
alignment = importlib.util.module_from_spec(specification)
specification.loader.exec_module(alignment)


def create_elf(page_alignment, elf_class=2, byte_order="<"):
    data = bytearray(128)
    data[:6] = b"\x7fELF" + bytes([elf_class, 1 if byte_order == "<" else 2])
    if elf_class == 2:
        struct.pack_into(byte_order + "Q", data, 32, 64)
        struct.pack_into(byte_order + "HH", data, 54, 56, 1)
        struct.pack_into(byte_order + "Q", data, 112, page_alignment)
    else:
        struct.pack_into(byte_order + "I", data, 28, 64)
        struct.pack_into(byte_order + "HH", data, 42, 32, 1)
        struct.pack_into(byte_order + "I", data, 92, page_alignment)
    struct.pack_into(byte_order + "I", data, 64, 1)
    return bytes(data)


class AndroidPageAlignmentTests(unittest.TestCase):
    def test_load_headers_preserve_alignment_for_both_classes_and_byte_orders(self):
        for elf_class in [1, 2]:
            for byte_order in ["<", ">"]:
                with self.subTest(elf_class=elf_class, byte_order=byte_order):
                    self.assertEqual([16384], alignment.read_load_alignments(create_elf(16384, elf_class, byte_order)))

    def test_apk_with_small_load_alignment_is_rejected(self):
        with tempfile.TemporaryDirectory(prefix="WatchListAlignment-") as folder:
            apk = pathlib.Path(folder) / "test.apk"
            with zipfile.ZipFile(apk, "w") as archive:
                archive.writestr("lib/arm64-v8a/libsynthetic.so", create_elf(4096))
            with self.assertRaisesRegex(ValueError, "smaller than 16 KB"):
                alignment.verify_apk(apk)

    def test_apk_with_valid_load_alignment_retains_every_library(self):
        with tempfile.TemporaryDirectory(prefix="WatchListAlignment-") as folder:
            apk = pathlib.Path(folder) / "test.apk"
            with zipfile.ZipFile(apk, "w") as archive:
                archive.writestr("lib/arm64-v8a/libsynthetic.so", create_elf(16384))
                archive.writestr("lib/x86_64/libsynthetic.so", create_elf(65536))
            self.assertEqual(2, len(alignment.verify_apk(apk)))

    def test_empty_apk_is_rejected_instead_of_claiming_compatibility(self):
        with tempfile.TemporaryDirectory(prefix="WatchListAlignment-") as folder:
            apk = pathlib.Path(folder) / "test.apk"
            with zipfile.ZipFile(apk, "w"):
                pass
            with self.assertRaisesRegex(ValueError, "no native libraries"):
                alignment.verify_apk(apk)

    def test_invalid_elf_is_rejected(self):
        with self.assertRaisesRegex(ValueError, "not an ELF"):
            alignment.read_load_alignments(b"not an ELF")


if __name__ == "__main__":
    unittest.main()
