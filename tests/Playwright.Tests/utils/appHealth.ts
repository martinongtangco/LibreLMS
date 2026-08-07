/**
 * Health-check utility for verifying the LMS app is responsive.
 *
 * Used by tests that need to confirm the app is running before
 * proceeding with assertions.
 */
export async function waitForAppReady(
  baseURL: string = 'http://localhost:5000',
  maxWaitMs: number = 120_000
): Promise<boolean> {
  const pollIntervalMs = 2_000;
  const startTime = Date.now();

  while (Date.now() - startTime < maxWaitMs) {
    try {
      const response = await fetch(baseURL, {
        method: 'GET',
        redirect: 'follow',
        signal: AbortSignal.timeout(5_000),
      });

      if (response.ok) {
        return true;
      }
    } catch {
      // Not ready yet
    }

    await new Promise((resolve) => setTimeout(resolve, pollIntervalMs));
  }

  return false;
}
