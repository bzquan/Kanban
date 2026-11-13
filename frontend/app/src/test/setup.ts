import '@testing-library/jest-dom/vitest'
import { installRequestShim } from './fetchStub'

// Must run before any test module imports the API client: openapi-fetch
// captures globalThis.Request when createClient() runs, and Node's Request
// (what jsdom exposes) rejects the root-relative URLs the client builds.
installRequestShim()
