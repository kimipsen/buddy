function toBase64Url(bytes: Uint8Array): string {
  let binary = '';

  for (const byte of bytes) {
    binary += String.fromCodePoint(byte);
  }

  return btoa(binary).replaceAll('+', '-').replaceAll('/', '_').replaceAll('=', '');
}

export function generateCodeVerifier(): string {
  const bytes = crypto.getRandomValues(new Uint8Array(32));

  return toBase64Url(bytes);
}

export async function generateCodeChallenge(verifier: string): Promise<string> {
  const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(verifier));

  return toBase64Url(new Uint8Array(digest));
}
