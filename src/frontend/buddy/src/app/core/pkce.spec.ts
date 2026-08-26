import { describe, expect, it, vi } from 'vitest';

import { generateCodeChallenge, generateCodeVerifier } from './pkce';

/** Decodes a base64url string back to raw bytes, independent of the implementation under test. */
function fromBase64Url(value: string): number[] {
  const padded = value + '='.repeat((4 - (value.length % 4)) % 4);
  const base64 = padded.replaceAll('-', '+').replaceAll('_', '/');
  const binary = atob(base64);

  return Array.from(binary, (char) => char.codePointAt(0)!);
}

describe('generateCodeVerifier', () => {
  it('returns a base64url string with no padding or reserved characters', () => {
    const verifier = generateCodeVerifier();

    expect(verifier).toMatch(/^[A-Za-z0-9_-]+$/);
    expect(verifier).not.toContain('+');
    expect(verifier).not.toContain('/');
    expect(verifier).not.toContain('=');
  });

  it('encodes 32 random bytes as a 43-character string, satisfying the RFC 7636 minimum length', () => {
    const verifier = generateCodeVerifier();

    expect(verifier).toHaveLength(43);
  });

  it('produces a different verifier on each call', () => {
    const first = generateCodeVerifier();
    const second = generateCodeVerifier();

    expect(first).not.toBe(second);
  });

  it('draws its randomness from crypto.getRandomValues with a 32-byte buffer', () => {
    const spy = vi.spyOn(crypto, 'getRandomValues');

    generateCodeVerifier();

    expect(spy).toHaveBeenCalledTimes(1);
    const buffer = spy.mock.calls[0][0] as Uint8Array;
    expect(buffer).toBeInstanceOf(Uint8Array);
    expect(buffer).toHaveLength(32);

    spy.mockRestore();
  });

  it('base64url-encodes the exact bytes returned by crypto.getRandomValues', () => {
    const spy = vi.spyOn(crypto, 'getRandomValues').mockImplementation(<T extends ArrayBufferView | null>(array: T): T => {
      const bytes = array as unknown as Uint8Array;
      for (let i = 0; i < bytes.length; i++) {
        bytes[i] = i * 8; // deterministic, spans the full byte range across 32 bytes
      }
      return array;
    });

    const verifier = generateCodeVerifier();
    const expectedBytes = Array.from({ length: 32 }, (_, i) => i * 8);

    expect(fromBase64Url(verifier)).toEqual(expectedBytes);

    spy.mockRestore();
  });
});

describe('generateCodeChallenge', () => {
  it('matches the official RFC 7636 Appendix B S256 test vector', async () => {
    const verifier = 'dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk';

    const challenge = await generateCodeChallenge(verifier);

    expect(challenge).toBe('E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM');
  });

  it('returns the base64url SHA-256 digest of the verifier for an arbitrary input', async () => {
    const verifier = 'a-known-verifier-value-for-testing-purposes';

    const challenge = await generateCodeChallenge(verifier);

    const expectedDigest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(verifier));
    const expectedBytes = Array.from(new Uint8Array(expectedDigest));

    expect(fromBase64Url(challenge)).toEqual(expectedBytes);
  });

  it('produces a base64url string with no padding or reserved characters', async () => {
    const challenge = await generateCodeChallenge('some-verifier');

    expect(challenge).toMatch(/^[A-Za-z0-9_-]+$/);
    expect(challenge).not.toContain('+');
    expect(challenge).not.toContain('/');
    expect(challenge).not.toContain('=');
  });

  it('is 43 characters long, matching a base64url-encoded 32-byte SHA-256 digest', async () => {
    const challenge = await generateCodeChallenge('some-verifier');

    expect(challenge).toHaveLength(43);
  });

  it('produces different challenges for different verifiers', async () => {
    const first = await generateCodeChallenge('verifier-one');
    const second = await generateCodeChallenge('verifier-two');

    expect(first).not.toBe(second);
  });

  it('is deterministic for the same verifier', async () => {
    const first = await generateCodeChallenge('same-verifier');
    const second = await generateCodeChallenge('same-verifier');

    expect(first).toBe(second);
  });

  it('produces a challenge that round-trips with a freshly generated verifier', async () => {
    const verifier = generateCodeVerifier();

    const challenge = await generateCodeChallenge(verifier);

    expect(challenge).not.toBe(verifier);
    expect(challenge).toMatch(/^[A-Za-z0-9_-]{43}$/);
  });
});
