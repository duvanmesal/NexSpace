"""Validate the shipped Windows icon and compare its image bytes with the PE resources."""

import argparse
import ctypes as ct
from ctypes import wintypes as w
from pathlib import Path
import struct

SIZES = (16, 24, 32, 48, 64, 128, 256)
ICONS = Path(__file__).resolve().parents[1] / "src-tauri/icons"


def require(condition, reason):
    if not condition:
        raise ValueError(reason)


def icon_directory(data, embedded=False):
    reserved, kind, count = struct.unpack_from("<HHH", data)
    require((reserved, kind, count) == (0, 1, len(SIZES)), "Directorio ICO inesperado.")
    entry = struct.Struct("<BBBBHHIH" if embedded else "<BBBBHHII")
    for index in range(count):
        width, height, _, _, _, bits, length, key = entry.unpack_from(data, 6 + index * entry.size)
        require(width == height and bits == 32, "Se esperaba un icono cuadrado de 32 bits.")
        yield width or 256, length, key


def icon_files():
    images = {}
    for size in SIZES:
        image = (ICONS / f"{size}x{size}.png").read_bytes()
        require(image[:16] == b"\x89PNG\r\n\x1a\n\x00\x00\x00\rIHDR" and
                struct.unpack_from(">II", image, 16) == (size, size) and image[24:26] == b"\x08\x06",
                f"Se esperaba un PNG RGBA de {size} px.")
        images[size] = image
    ico = (ICONS / "icon.ico").read_bytes()
    entries = {size: ico[offset:offset + count] for size, count, offset in icon_directory(ico)}
    require(entries == images, "El ICO y los PNG difieren.")
    return images


def windows_decodes_icon():
    user = ct.WinDLL("user32", use_last_error=True)
    user.LoadImageW.argtypes = [w.HINSTANCE, w.LPCWSTR, w.UINT, ct.c_int, ct.c_int, w.UINT]
    user.LoadImageW.restype = w.HANDLE
    user.DestroyIcon.argtypes = [w.HICON]
    user.DestroyIcon.restype = w.BOOL
    for size in SIZES:
        handle = user.LoadImageW(None, str(ICONS / "icon.ico"), 1, size, size, 0x10)  # IMAGE_ICON, LR_LOADFROMFILE
        require(handle, f"Windows no pudo abrir el ICO a {size} px.")
        user.DestroyIcon(handle)


def exe_icons(path):
    kernel = ct.WinDLL("kernel32", use_last_error=True)
    visitor = ct.WINFUNCTYPE(w.BOOL, w.HMODULE, ct.c_void_p, ct.c_void_p, w.LPARAM)
    signatures = {
        "LoadLibraryExW": (w.HMODULE, w.LPCWSTR, w.HANDLE, w.DWORD),
        "FreeLibrary": (w.BOOL, w.HMODULE),
        "EnumResourceNamesW": (w.BOOL, w.HMODULE, ct.c_void_p, visitor, w.LPARAM),
        "FindResourceW": (w.HANDLE, w.HMODULE, ct.c_void_p, ct.c_void_p),
        "SizeofResource": (w.DWORD, w.HMODULE, w.HANDLE),
        "LoadResource": (w.HANDLE, w.HMODULE, w.HANDLE),
        "LockResource": (ct.c_void_p, w.HANDLE),
    }
    for name, (result, *arguments) in signatures.items():
        api = getattr(kernel, name)
        api.restype, api.argtypes = result, arguments
    module = kernel.LoadLibraryExW(str(path.resolve(strict=True)), None, 0x22)  # Data and image resources only
    if not module:
        raise ct.WinError(ct.get_last_error())

    def read(name, kind):
        resource = kernel.FindResourceW(module, name, kind)
        require(resource, f"Falta el recurso {kind}.")
        length = kernel.SizeofResource(module, resource)
        address = kernel.LockResource(kernel.LoadResource(module, resource))
        require(address and length, f"El recurso {kind} está vacío.")
        return ct.string_at(address, length)

    groups = []
    errors = []

    @visitor
    def collect(_module, _kind, name, _data):
        try:
            frames = {}
            for size, length, key in icon_directory(read(name, 14), embedded=True):
                data = read(key, 3)
                require(len(data) == length, f"El fotograma {size} del EXE tiene longitud incorrecta.")
                frames[size] = data
            groups.append(frames)
        except ValueError as error:
            errors.append(str(error))
            return False
        return True

    try:
        completed = kernel.EnumResourceNamesW(module, 14, collect, 0)
        require(completed and groups, errors[0] if errors else "No se encontró RT_GROUP_ICON en el EXE.")
        return groups
    finally:
        kernel.FreeLibrary(module)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--exe", type=Path)
    args = parser.parse_args()
    png = icon_files()
    windows_decodes_icon()
    if args.exe:
        require(all(group == png for group in exe_icons(args.exe)), "El ejecutable incluye un grupo de iconos diferente.")
    print("PNG RGBA e ICO de 16, 24, 32, 48, 64, 128 y 256 px verificados." +
          (" Los siete fotogramas coinciden con los recursos del ejecutable." if args.exe else ""))
