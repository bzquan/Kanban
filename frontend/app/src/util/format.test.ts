import { describe, expect, it } from 'vitest'

import { format0 } from './format'

describe('format0', () => {
  it('substitutes the first {0} placeholder', () => {
    expect(format0('Name：{0} ', 'Alpha')).toBe('Name：Alpha ')
  })

  it('renders $-sequences in the value literally', () => {
    // String.replace treats $&, $', $` and $$ specially in the *replacement
    // string*; a value like `A$'B` must still come out verbatim.
    expect(format0('Detail：{0}', "A$'B$$C")).toBe("Detail：A$'B$$C")
  })
})
