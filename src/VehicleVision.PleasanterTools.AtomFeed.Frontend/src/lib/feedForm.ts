import type { Feed, FeedInput } from './types';

/** フォームの入力（すべて文字列。空は「指定なし」）。 */
export interface FeedFormValues {
  name: string;
  siteId: string;
  language: string;
  apiKeyUserTimeZoneId: string;
  apiKey: string;
  titleColumn: string;
  titleTemplate: string;
  updatedColumn: string;
  updatedFormat: string;
  publishedColumn: string;
  publishedFormat: string;
  contentColumn: string;
  authorColumn: string;
  filters: string;
  /** 範囲。無制限なら値は使わない。 */
  pastUnlimited: boolean;
  pastDays: string;
  maxItemsUnlimited: boolean;
  maxItems: string;
}

/** 本体の既定（`FeedMapping` の既定値と同じ）。 */
export function emptyFeedForm(language: string): FeedFormValues {
  return {
    name: '',
    siteId: '',
    language,
    apiKeyUserTimeZoneId: '',
    apiKey: '',
    titleColumn: 'ItemTitle',
    titleTemplate: '',
    updatedColumn: 'UpdatedTime',
    updatedFormat: '',
    publishedColumn: 'CreatedTime',
    publishedFormat: '',
    contentColumn: 'Body',
    authorColumn: '',
    filters: '',
    // 新しいフィードの既定: 過去 90 日・最大 100 件（サーバの既定と同じ）
    pastUnlimited: false,
    pastDays: '90',
    maxItemsUnlimited: false,
    maxItems: '100',
  };
}

/** 既存のフィードから、編集フォームの初期値を作る。 */
export function formFromFeed(feed: Feed): FeedFormValues {
  const m = feed.mapping;
  return {
    name: feed.name,
    siteId: String(feed.siteId),
    language: feed.language,
    apiKeyUserTimeZoneId: feed.apiKeyUserTimeZoneId ?? '',
    apiKey: '',
    titleColumn: m.titleColumn,
    titleTemplate: m.titleTemplate ?? '',
    updatedColumn: m.updatedColumn,
    updatedFormat: m.updatedFormat ?? '',
    publishedColumn: m.publishedColumn ?? '',
    publishedFormat: m.publishedFormat ?? '',
    contentColumn: m.contentColumn ?? '',
    authorColumn: m.authorColumn ?? '',
    filters: filtersToLines(feed.filters),
    pastUnlimited: feed.range.pastDays === null,
    pastDays: feed.range.pastDays === null ? '' : String(feed.range.pastDays),
    maxItemsUnlimited: feed.range.maxItems === null,
    maxItems: feed.range.maxItems === null ? '' : String(feed.range.maxItems),
  };
}

/** `列名=値` の行にする。 */
export function filtersToLines(filters: Record<string, string>): string {
  return Object.entries(filters)
    .map(([k, v]) => `${k}=${v}`)
    .join('\n');
}

/** `列名=値` の行から絞り込みを読む。空行は飛ばす。**最初の `=` で分ける**（値に `=` を含められる）。不正な行は null。 */
export function linesToFilters(text: string): Record<string, string> | null {
  const result: Record<string, string> = {};
  for (const raw of text.split(/\r?\n/)) {
    const line = raw.trim();
    if (line === '') {continue;}
    const at = line.indexOf('=');
    if (at <= 0) {return null;}
    result[line.slice(0, at).trim()] = line.slice(at + 1).trim();
  }

  return result;
}

const blankToNull = (v: string): string | null => (v.trim() === '' ? null : v.trim());

/**
 * フォームから送る内容を作る。**入力に問題があれば null**（絞り込みの行が不正・サイト ID が数でない）。
 * 画面で見つけられるのはここまで。列名・書式の細かい検証はサーバが行い、理由を返す。
 */
/** 日数（0〜36500 の整数）を読む。無制限なら null。読めなければ undefined。 */
function readDays(unlimited: boolean, text: string): number | null | undefined {
  if (unlimited) {return null;}
  const value = Number(text);
  return text.trim() !== '' && Number.isInteger(value) && value >= 0 && value <= 36500 ? value : undefined;
}

/** 件数（1〜100000 の整数）を読む。無制限なら null。読めなければ undefined。 */
function readCount(unlimited: boolean, text: string): number | null | undefined {
  if (unlimited) {return null;}
  const value = Number(text);
  return text.trim() !== '' && Number.isInteger(value) && value >= 1 && value <= 100000 ? value : undefined;
}

export function toFeedInput(values: FeedFormValues): FeedInput | null {
  const siteId = Number(values.siteId);
  const filters = linesToFilters(values.filters);
  const pastDays = readDays(values.pastUnlimited, values.pastDays);
  const maxItems = readCount(values.maxItemsUnlimited, values.maxItems);
  if (
    !Number.isInteger(siteId) ||
    siteId <= 0 ||
    filters === null ||
    pastDays === undefined ||
    maxItems === undefined
  ) {
    return null;
  }

  return {
    name: values.name.trim(),
    siteId,
    language: values.language,
    apiKeyUserTimeZoneId: blankToNull(values.apiKeyUserTimeZoneId),
    apiKey: blankToNull(values.apiKey),
    pastDays,
    maxItems,
    filters,
    mapping: {
      titleColumn: values.titleColumn.trim() || 'ItemTitle',
      titleTemplate: blankToNull(values.titleTemplate),
      updatedColumn: values.updatedColumn.trim() || 'UpdatedTime',
      updatedFormat: blankToNull(values.updatedFormat),
      publishedColumn: blankToNull(values.publishedColumn),
      publishedFormat: blankToNull(values.publishedFormat),
      contentColumn: blankToNull(values.contentColumn),
      authorColumn: blankToNull(values.authorColumn),
    },
  };
}
