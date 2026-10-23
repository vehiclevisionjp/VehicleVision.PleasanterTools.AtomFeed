import { afterEach, describe, expect, it, vi } from 'vitest';
import { api } from './api';

vi.mock('./i18n/state.svelte', () => ({ language: () => 'ja' }));

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('2 要素の登録開始', () => {
  it('再登録では現在のパスワードを既存の経路へ送り、同じサイトの cookie を使う', async () => {
    const fetch = vi.fn().mockResolvedValue(Response.json({ secret: 'SECRET', uri: 'otpauth://totp/test' }));
    vi.stubGlobal('fetch', fetch);

    expect((await api.totpBegin(' current password ')).ok).toBe(true);
    expect(fetch).toHaveBeenCalledWith('/admin/api/me/totp/begin', expect.objectContaining({
      method: 'POST',
      credentials: 'same-origin',
      headers: { 'Content-Type': 'application/json', 'Accept-Language': 'ja' },
      body: JSON.stringify({ password: ' current password ' }),
    }));
  });

  it('初回登録はパスワードなしで同じ経路を使う', async () => {
    const fetch = vi.fn().mockResolvedValue(Response.json({ secret: 'SECRET', uri: 'otpauth://totp/test' }));
    vi.stubGlobal('fetch', fetch);

    await api.totpBegin();

    expect(fetch).toHaveBeenCalledWith('/admin/api/me/totp/begin', expect.objectContaining({ body: '{}' }));
  });

  it.each([401, 423, 429])('拒否 %i の文言を画面へ渡す', async (status) => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(Response.json({ error: '本人確認が必要です。' }, { status })));

    expect(await api.totpBegin('wrong')).toEqual({ ok: false, status, message: '本人確認が必要です。', code: undefined });
  });
});
