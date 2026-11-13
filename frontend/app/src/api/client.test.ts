import { afterEach, expect, it } from 'vitest'

import { calls, installFetch, restoreFetch } from '../test/fetchStub'
import { ApiError, boardsApi, cardsApi, intVal } from './client'

afterEach(() => restoreFetch())

it('boardsApi.list GETs /api/boards and unwraps array', async () => {
  const board = { id: 'b1', seqNo: '1', title: 'Board 1' }
  installFetch({ 'GET /api/boards': () => [board] })

  const result = await boardsApi.list()

  expect(result).toEqual([board])
  expect(calls).toEqual([{ method: 'GET', url: '/api/boards', body: undefined }])
})

it('cardsApi.list encodes CardsQuery into query string', async () => {
  installFetch({ 'GET /api/boards/b1/cards': () => [] })

  await cardsApi.list('b1', {
    processStepSeqNo: 2,
    isWip: false,
    filter: 'abc',
    onBoard: 'back',
  })

  expect(calls).toHaveLength(1)
  expect(calls[0].method).toBe('GET')
  expect(calls[0].url).toBe(
    '/api/boards/b1/cards?processStepSeqNo=2&isWip=false&filter=abc&onBoard=back',
  )
})

it('cardsApi.move PATCHes /api/cards/{id}/move with body', async () => {
  installFetch({
    'PATCH /api/cards/c1/move': () => new Response(null, { status: 204 }),
  })

  await expect(
    cardsApi.move('c1', { processStepSeqNo: 2, isWip: true, index: 1 }),
  ).resolves.toBeUndefined()

  expect(calls).toEqual([
    {
      method: 'PATCH',
      url: '/api/cards/c1/move',
      body: { processStepSeqNo: 2, isWip: true, index: 1 },
    },
  ])
})

it('boardsApi.create sends {uiLanguage} and throws ApiError on 400', async () => {
  installFetch({
    'POST /api/boards': () => new Response(null, { status: 400 }),
  })

  const error = await boardsApi.create('ja').catch((e: unknown) => e)

  expect(error).toBeInstanceOf(ApiError)
  expect((error as ApiError).status).toBe(400)
  expect(calls).toEqual([
    { method: 'POST', url: '/api/boards', body: { uiLanguage: 'ja' } },
  ])
})

it('intVal parses "7", 7 and undefined to 7, 7, 0', () => {
  expect(intVal('7')).toBe(7)
  expect(intVal(7)).toBe(7)
  expect(intVal(undefined)).toBe(0)
})
