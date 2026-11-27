using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using Aeron.Contracts;

namespace Aeron.Infrastructure;

/// <summary>
/// Windows DPAPI protector in <c>CurrentUser</c> scope using built-in <c>crypt32.dll</c>.
///
/// * Protection is bound to the current Windows user profile, so another account on the
///   same device cannot unprotect the bytes.
/// * The optional entropy value pins the blob to AERON so unrelated DPAPI blobs from the
///   same user cannot be swapped in.
/// * Every failure is reported as failure; the caller must fail closed rather than store
///   or accept unprotected material.
/// * Plaintext copies made for the unmanaged call are zeroed before the buffers are
///   released, and DPAPI's own output buffers are zeroed before being freed.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class DpapiDataProtector : IDataProtector
{
    /// <summary><c>CRYPTPROTECT_UI_FORBIDDEN</c>: never show a UI prompt.</summary>
    private const int CryptProtectUiForbidden = 0x1;

    private static readonly byte[] Entropy = "AERON-VAULT-DPAPI-V1"u8.ToArray();

    public bool TryProtect(ReadOnlySpan<byte> plaintext, out byte[] protectedBytes)
    {
        protectedBytes = [];

        if (plaintext.IsEmpty)
        {
            return false;
        }

        byte[]? inputCopy = null;
        nint inputBuffer = 0;
        nint entropyBuffer = 0;
        var outputBlob = default(DpapiDataProtector.DataBlob);

        try
        {
            inputCopy = plaintext.ToArray();
            inputBuffer = Marshal.AllocHGlobal(inputCopy.Length);
            Marshal.Copy(inputCopy, 0, inputBuffer, inputCopy.Length);

            entropyBuffer = Marshal.AllocHGlobal(Entropy.Length);
            Marshal.Copy(Entropy, 0, entropyBuffer, Entropy.Length);

            var inputBlob = new DataBlob(inputBuffer, inputCopy.Length);
            var entropyBlob = new DataBlob(entropyBuffer, Entropy.Length);

            if (!DpapiNativeMethods.CryptProtectData(
                    ref inputBlob,
                    "AERON",
                    ref entropyBlob,
                    nint.Zero,
                    nint.Zero,
                    CryptProtectUiForbidden,
                    out outputBlob))
            {
                return false;
            }

            if (outputBlob.Data == nint.Zero || outputBlob.Size <= 0)
            {
                return false;
            }

            protectedBytes = new byte[outputBlob.Size];
            Marshal.Copy(outputBlob.Data, protectedBytes, 0, outputBlob.Size);
            return true;
        }
        catch (Exception)
        {
            protectedBytes = [];
            return false;
        }
        finally
        {
            if (inputCopy is not null)
            {
                CryptographicOperations.ZeroMemory(inputCopy);
            }

            if (outputBlob.Data != nint.Zero)
            {
                ZeroAndFreeLocal(outputBlob.Data, outputBlob.Size);
            }

            if (inputBuffer != nint.Zero)
            {
                Marshal.FreeHGlobal(inputBuffer);
            }

            if (entropyBuffer != nint.Zero)
            {
                Marshal.FreeHGlobal(entropyBuffer);
            }
        }
    }

    public bool TryUnprotect(ReadOnlySpan<byte> protectedBytes, out byte[] plaintext)
    {
        plaintext = [];

        if (protectedBytes.IsEmpty)
        {
            return false;
        }

        byte[]? inputCopy = null;
        nint inputBuffer = 0;
        nint entropyBuffer = 0;
        nint description = nint.Zero;
        var outputBlob = default(DpapiDataProtector.DataBlob);

        try
        {
            inputCopy = protectedBytes.ToArray();
            inputBuffer = Marshal.AllocHGlobal(inputCopy.Length);
            Marshal.Copy(inputCopy, 0, inputBuffer, inputCopy.Length);

            entropyBuffer = Marshal.AllocHGlobal(Entropy.Length);
            Marshal.Copy(Entropy, 0, entropyBuffer, Entropy.Length);

            var inputBlob = new DataBlob(inputBuffer, inputCopy.Length);
            var entropyBlob = new DataBlob(entropyBuffer, Entropy.Length);

            if (!DpapiNativeMethods.CryptUnprotectData(
                    ref inputBlob,
                    out description,
                    ref entropyBlob,
                    nint.Zero,
                    nint.Zero,
                    CryptProtectUiForbidden,
                    out outputBlob))
            {
                return false;
            }

            if (outputBlob.Data == nint.Zero || outputBlob.Size <= 0)
            {
                return false;
            }

            plaintext = new byte[outputBlob.Size];
            Marshal.Copy(outputBlob.Data, plaintext, 0, outputBlob.Size);
            return true;
        }
        catch (Exception)
        {
            plaintext = [];
            return false;
        }
        finally
        {
            // DPAPI allocates the returned description with LocalAlloc.
            if (description != nint.Zero)
            {
                Marshal.ZeroFreeGlobalAllocUnicode(description);
            }

            if (outputBlob.Data != nint.Zero)
            {
                ZeroAndFreeLocal(outputBlob.Data, outputBlob.Size);
            }

            if (inputBuffer != nint.Zero)
            {
                Marshal.FreeHGlobal(inputBuffer);
            }

            if (entropyBuffer != nint.Zero)
            {
                Marshal.FreeHGlobal(entropyBuffer);
            }
        }
    }

    /// <summary>Overwrites a <c>LocalAlloc</c> buffer allocated by DPAPI, then frees it.</summary>
    private static void ZeroAndFreeLocal(nint buffer, int size)
    {
        if (size > 0)
        {
            var zeroes = new byte[size];
            Marshal.Copy(zeroes, 0, buffer, size);
        }

        Marshal.ZeroFreeGlobalAllocUnicode(buffer);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DataBlob
    {
        public int Size;

        public nint Data;

        public DataBlob(nint data, int size)
        {
            Size = size;
            Data = data;
        }
    }
}

/// <summary>
/// Built-in <c>crypt32.dll</c> bindings. No package dependency is introduced.
/// </summary>
internal static partial class DpapiNativeMethods
{
    [LibraryImport("crypt32.dll", EntryPoint = "CryptProtectData", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CryptProtectData(
        ref DpapiDataProtector.DataBlob dataIn,
        string? description,
        ref DpapiDataProtector.DataBlob optionalEntropy,
        nint reserved,
        nint promptStruct,
        int flags,
        out DpapiDataProtector.DataBlob dataOut);

    [LibraryImport("crypt32.dll", EntryPoint = "CryptUnprotectData", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CryptUnprotectData(
        ref DpapiDataProtector.DataBlob dataIn,
        out nint description,
        ref DpapiDataProtector.DataBlob optionalEntropy,
        nint reserved,
        nint promptStruct,
        int flags,
        out DpapiDataProtector.DataBlob dataOut);
}
