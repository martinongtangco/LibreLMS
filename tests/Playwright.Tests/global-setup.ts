/**
 * Global setup: poll the app until it returns HTTP 200.
 *
 * The LMS calls EnsureDeleted() + Migrate() + Seed() on startup,
 * which can take 15-30 seconds. This setup waits patiently before
 * any test runs.
 */
async function globalSetup() {
  const baseURL = process.env.APP_BASE_URL || 'http://localhost:5000';
  const maxWaitMs = 120_000; // 2 minutes
  const pollIntervalMs = 2_000; // 2 seconds
  const startTime = Date.now();

  console.log(`[global-setup] Waiting for app at ${baseURL}...`);

  while (Date.now() - startTime < maxWaitMs) {
    try {
      const response = await fetch(baseURL, {
        method: 'GET',
        redirect: 'follow',
        signal: AbortSignal.timeout(5_000),
      });

      if (response.ok) {
        console.log(`[global-setup] App is ready (${Date.now() - startTime}ms)`);
        return;
      }
    } catch {
      // App not ready yet; keep polling
    }

    await new Promise((resolve) => setTimeout(resolve, pollIntervalMs));
  }

  throw new Error(
    `[global-setup] App at ${baseURL} did not become healthy within ${maxWaitMs}ms`
  );
}

export default globalSetup;
