<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useRouter } from 'vue-router';
import { useApi } from '../../core/api/api';
import { toApiError, type ApiError } from '../../core/api/api-error';
import type { DiaryEntry, DiaryQuery, PagedResponse } from '../../core/api/models';
import { formatDate, formatDay, formatMonth, monthOf, yearOf } from '../../shared/dates';
import ErrorNotification from '../../shared/ErrorNotification.vue';
import Pagination from '../../shared/Pagination.vue';
import { mergeQuery } from '../../shared/query';
import Rating from '../../shared/Rating.vue';
import TagList from '../../shared/TagList.vue';

const PAGE_SIZE = 25;

interface MonthGroup {
  /** "YYYY-MM" */
  key: string;
  label: string;
  entries: DiaryEntry[];
}

/** The filters, from the route's query parameters. */
const props = defineProps<{
  from?: string;
  to?: string;
  tag?: string;
  page?: string;
}>();

const router = useRouter();
const api = useApi();

const query = computed<DiaryQuery>(() => ({
  from: props.from,
  to: props.to,
  tag: props.tag?.trim() || undefined,
  page: Number(props.page) || 1,
  pageSize: PAGE_SIZE,
}));

const result = ref<PagedResponse<DiaryEntry> | null>(null);
/** The current page's entries grouped by month, in the order the API returned them. */
const groups = computed<MonthGroup[]>(() => {
  const groups: MonthGroup[] = [];
  for (const entry of result.value?.items ?? []) {
    const key = monthOf(entry.watchedOn);
    let group = groups.at(-1);
    if (group?.key !== key) {
      group = { key, label: formatMonth(entry.watchedOn), entries: [] };
      groups.push(group);
    }
    group.entries.push(entry);
  }
  return groups;
});
const loading = ref(true);
const error = ref<ApiError | null>(null);

/** Numbers the requests, so that the answer to an outdated one is ignored. */
let latestLoad = 0;

/** Loads the current page of the diary. */
async function load(): Promise<void> {
  const thisLoad = ++latestLoad;
  loading.value = true;
  let loaded: PagedResponse<DiaryEntry> | null;
  try {
    loaded = await api.diary.list(query.value);
    if (thisLoad !== latestLoad) {
      return;
    }
    error.value = null;
  } catch (failure) {
    if (thisLoad !== latestLoad) {
      return;
    }
    error.value = toApiError(failure);
    loaded = null;
  }

  // E.g. after deleting the only entry on the last page.
  if (loaded && loaded.items.length === 0 && loaded.page > 1) {
    goToPage(Math.max(1, Math.ceil(loaded.totalCount / loaded.pageSize)));
    return;
  }
  result.value = loaded;
  loading.value = false;
}

watch(query, load, { immediate: true });

function onFilter(key: 'from' | 'to' | 'tag', value: string): void {
  router.push({
    query: mergeQuery(router.currentRoute.value.query, {
      [key]: value || undefined,
      page: undefined,
    }),
  });
}

function clearFilters(): void {
  router.push({ query: {} });
}

function goToPage(page: number): void {
  router.push({ query: mergeQuery(router.currentRoute.value.query, { page }) });
}

async function deleteEntry(entry: DiaryEntry): Promise<void> {
  if (
    !confirm(
      `Delete the diary entry for "${entry.movie.title}" from ${formatDate(entry.watchedOn)}?`,
    )
  ) {
    return;
  }
  try {
    await api.diary.delete(entry.id);
  } catch (failure) {
    error.value = toApiError(failure);
    return;
  }
  void load();
}
</script>

<template>
  <div class="level is-mobile">
    <div class="level-left">
      <h1 class="title level-item">Diary</h1>
    </div>
    <div class="level-right">
      <RouterLink class="button is-primary level-item" to="/diary/new">Log a viewing</RouterLink>
    </div>
  </div>

  <div class="box">
    <div class="columns is-mobile is-multiline is-align-items-flex-end">
      <div class="column is-half-mobile is-3-tablet">
        <div class="field">
          <label class="label" for="from">From</label>
          <div class="control">
            <input
              id="from"
              class="input"
              type="date"
              :value="query.from ?? ''"
              :max="query.to ?? ''"
              @change="onFilter('from', ($event.target as HTMLInputElement).value)"
            />
          </div>
        </div>
      </div>
      <div class="column is-half-mobile is-3-tablet">
        <div class="field">
          <label class="label" for="to">To</label>
          <div class="control">
            <input
              id="to"
              class="input"
              type="date"
              :value="query.to ?? ''"
              :min="query.from ?? ''"
              @change="onFilter('to', ($event.target as HTMLInputElement).value)"
            />
          </div>
        </div>
      </div>
      <div class="column is-half-mobile is-3-tablet">
        <div class="field">
          <label class="label" for="tag">Tag</label>
          <div class="control">
            <input
              id="tag"
              class="input"
              type="text"
              autocomplete="off"
              :value="query.tag ?? ''"
              @change="onFilter('tag', ($event.target as HTMLInputElement).value.trim())"
            />
          </div>
        </div>
      </div>
      <div v-if="query.from || query.to || query.tag" class="column is-narrow">
        <button class="button is-light" @click="clearFilters">Clear</button>
      </div>
    </div>
  </div>

  <ErrorNotification :error="error" @dismiss="error = null" />

  <template v-if="result">
    <p class="mb-4 has-text-grey">
      {{ result.totalCount }} {{ result.totalCount === 1 ? 'entry' : 'entries' }}
      <span v-if="loading" class="ml-2">· loading…</span>
    </p>

    <div v-if="result.items.length" class="table-container">
      <table class="table is-fullwidth is-hoverable">
        <thead>
          <tr>
            <th>Day</th>
            <th>Movie</th>
            <th>Rating</th>
            <th class="is-hidden-mobile">Review</th>
            <th></th>
          </tr>
        </thead>
        <tbody v-for="group in groups" :key="group.key">
          <tr class="has-background-light">
            <th colspan="5" class="has-text-weight-semibold">{{ group.label }}</th>
          </tr>
          <tr v-for="entry in group.entries" :key="entry.id">
            <td class="is-nowrap">{{ formatDay(entry.watchedOn) }}</td>
            <td>
              <RouterLink :to="`/movies/${entry.movie.id}`" class="has-text-weight-semibold">{{
                entry.movie.title
              }}</RouterLink>
              <span v-if="entry.movie.releaseDate" class="has-text-grey">
                ({{ yearOf(entry.movie.releaseDate) }})</span
              >
              <span v-if="entry.isRewatch" class="tag is-light ml-1" title="Rewatch">↻</span>
              <span v-if="entry.liked" class="has-text-danger ml-1" title="Liked">♥</span>
              <TagList :tags="entry.tags" route="/diary" :active="query.tag" />
            </td>
            <td><Rating :half-stars="entry.rating" /></td>
            <td class="is-hidden-mobile pre-wrap">{{ entry.review }}</td>
            <td>
              <div class="buttons is-right are-small is-flex-wrap-nowrap">
                <RouterLink class="button" :to="`/diary/${entry.id}/edit`">Edit</RouterLink>
                <button class="button is-danger is-outlined" @click="deleteEntry(entry)">
                  Delete
                </button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
    <div v-else class="notification">
      No diary entries
      <template v-if="query.tag">for this filter.</template>
      <template v-else-if="query.from || query.to">in this period.</template>
      <template v-else
        >yet. <RouterLink to="/diary/new">Log your first viewing</RouterLink>.</template
      >
    </div>

    <Pagination
      :page="result.page"
      :page-size="result.pageSize"
      :total-count="result.totalCount"
      @page-change="goToPage"
    />
  </template>
  <progress v-else-if="loading" class="progress is-small is-primary" max="100"></progress>
</template>
