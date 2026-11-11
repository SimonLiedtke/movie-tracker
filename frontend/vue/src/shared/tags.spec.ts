import { describe, expect, it } from 'vitest';
import { MAX_TAGS, MAX_TAG_LENGTH, formatTags, parseTags, tagsError } from './tags';

describe('parseTags', () => {
  it('splits the text at commas and trims the tags', () => {
    expect(parseTags(' horror,  date night ,cinema')).toEqual(['horror', 'date night', 'cinema']);
  });

  it('leaves out blank tags', () => {
    expect(parseTags(', ,horror,,')).toEqual(['horror']);
    expect(parseTags('')).toEqual([]);
  });
});

describe('formatTags', () => {
  it('joins the tags with commas', () => {
    expect(formatTags(['horror', 'date night'])).toBe('horror, date night');
    expect(formatTags([])).toBe('');
  });
});

describe('tagsError', () => {
  it('accepts normal tags', () => {
    expect(tagsError('horror, date night')).toBeNull();
    expect(tagsError('')).toBeNull();
  });

  it('refuses a tag that is too long', () => {
    expect(tagsError('x'.repeat(MAX_TAG_LENGTH))).toBeNull();
    expect(tagsError('x'.repeat(MAX_TAG_LENGTH + 1))).toContain(`${MAX_TAG_LENGTH} characters`);
  });

  it('refuses too many different tags, but counts a tag in two spellings once', () => {
    const tags = Array.from({ length: MAX_TAGS }, (_, i) => `tag ${i}`);
    expect(tagsError(tags.join(','))).toBeNull();
    expect(tagsError([...tags, 'TAG  0'].join(','))).toBeNull();
    expect(tagsError([...tags, 'one more'].join(','))).toContain(`${MAX_TAGS} different`);
  });
});
