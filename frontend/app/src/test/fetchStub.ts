// Test-only fetch stub.
//
// installFetch() swaps globalThis.fetch with a scripted handler table so tests
// can assert on the requests the code under test actually sent (recorded in
// `calls`) and pin canned responses. Handlers are keyed by "METHOD /path"
// (path without query); returning a plain object serves it as a 200 JSON
// response, returning a Response passes it through.
//
// installRequestShim() replaces globalThis.Request with a subclass that
// accepts root-relative URLs ('/api/boards'): Node's Request — the one jsdom
// exposes here — rejects them, and openapi-fetch captures globalThis.Request
// once, when createClient() runs. That is why src/test/setup.ts installs the
// shim before any test module imports the client.

export interface RecordedCall {
  method: string
  /** pathname + search of the request URL, e.g. '/api/boards?filter=abc'. */
  url: string
  /** JSON-parsed request body, raw text when not JSON, undefined when absent. */
  body: unknown
}

export type FetchHandler = (req: Request) => Response | object

/** Requests recorded by the stub, newest last. Cleared on each installFetch(). */
export const calls: RecordedCall[] = []

let savedFetch: typeof globalThis.fetch | null = null
let requestShimInstalled = false

function absoluteBase(): string {
  return typeof globalThis.location === 'undefined'
    ? 'http://localhost/'
    : globalThis.location.href
}

export function installRequestShim(): void {
  if (requestShimInstalled) return
  const NativeRequest = globalThis.Request
  class RelativeRequest extends NativeRequest {
    constructor(input: RequestInfo | URL, init?: RequestInit) {
      super(
        typeof input === 'string' && input.startsWith('/')
          ? new URL(input, absoluteBase()).toString()
          : input,
        init,
      )
    }
  }
  globalThis.Request = RelativeRequest
  requestShimInstalled = true
}

export function installFetch(handlers: Record<string, FetchHandler>): void {
  if (savedFetch) restoreFetch()
  calls.length = 0
  savedFetch = globalThis.fetch

  const RequestImpl = globalThis.Request
  globalThis.fetch = async (
    input: RequestInfo | URL,
    init?: RequestInit,
  ): Promise<Response> => {
    const req =
      input instanceof RequestImpl ? input : new RequestImpl(input, init)

    const parsed = new URL(req.url)
    const key = `${req.method} ${parsed.pathname}`

    const text = await req.clone().text()
    let body: unknown
    if (text !== '') {
      try {
        body = JSON.parse(text)
      } catch {
        body = text
      }
    }
    calls.push({
      method: req.method,
      url: parsed.pathname + parsed.search,
      body,
    })

    const handler = handlers[key]
    if (!handler) {
      throw new Error(`fetchStub: no handler for "${key}"`)
    }
    const result = await handler(req)
    if (result instanceof Response) {
      return result
    }
    return new Response(JSON.stringify(result), {
      status: 200,
      headers: { 'content-type': 'application/json' },
    })
  }
}

export function restoreFetch(): void {
  if (!savedFetch) return
  globalThis.fetch = savedFetch
  savedFetch = null
  calls.length = 0
}
