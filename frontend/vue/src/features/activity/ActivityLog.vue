<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useRouter } from 'vue-router';
import { useApi } from '../../core/api/api';
import { toApiError, type ApiError } from '../../core/api/api-error';
import type {
  ActivityLogEntry,
  ActivityType,
  PageQuery,
  PagedResponse,
} from '../../core/api/models';
import { formatDateTime } from '../../shared/dates';
import ErrorNotification from '../../shared/ErrorNotification.vue';
import Pagination from '../../shared/Pagination.vue';
import { mergeQuery } from '../../shared/query';

const PAGE_SIZE = 50;

/** What each kind of action is called in the log. */
const LABELS: Record<ActivityType, string> = {
  diaryEntryAdded: 'Logged a viewing',
  diaryEntryUpdated: 'Changed a viewing',
  diaryEntryDeleted: 'Deleted a viewing',
  watchlistItemAdded: 'Added to the watchlist',
  watchlistNoteChanged: 'Changed a watchlist note',
  watchlistItemRemoved: 'Removed from the watchlist',
  movieImported: 'Imported a movie from TMDB',
  movieAdded: 'Added a movie manually',
  movieUpdated: 'Changed a movie',
  movieDeleted: 'Deleted a movie',
  letterboxdImported: 'Imported from Letterboxd',
};

const label = (type: ActivityType): string => LABELS[type] ?? type;

/** The page, from the route's query parameters. */
const props = defineProps<{
  page?: string;
}>();

const router = useRouter();
const api = useApi();

const query = computed<PageQuery>(() => ({ page: Number(props.page) || 1, pageSize: PAGE_SIZE }));

const result = ref<PagedResponse<ActivityLogEntry> | null>(null);
const loading = ref(true);
const error = ref<ApiError | null>(null);

/** Numbers the requests, so that the answer to an outdated one is ignored. */
let latestLoad = 0;

/** Loads the current page of the log. */
async function load(): Promise<void> {
  const thisLoad = ++latestLoad;
  loading.value = true;
  let loaded: PagedResponse<ActivityLogEntry> | null;
  try {
    loaded = await api.activityLog.list(query.value);
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

  // The page is past the end, e.g. from an old bookmark.
  if (loaded && loaded.items.length === 0 && loaded.page > 1) {
    goToPage(Math.max(1, Math.ceil(loaded.totalCount / loaded.pageSize)));
    return;
  }
  result.value = loaded;
  loading.value = false;
}

watch(query, load, { immediate: true });

function goToPage(page: number): void {
  router.push({
    query: mergeQuery(router.currentRoute.value.query, { page: page > 1 ? page : undefined }),
  });
}
</script>

<template>
  <h1 class="title">Activity log</h1>
  <p class="subtitle is-6 has-text-grey">
    Everything you did that changed your data, most recent first.
  </p>

  <ErrorNotification :error="error" @dismiss="error = null" />

  <template v-if="result">
    <p class="mb-4 has-text-grey">
      {{ result.totalCount }} {{ result.totalCount === 1 ? 'action' : 'actions' }}
      <span v-if="loading" class="ml-2">· loading…</span>
    </p>

    <div v-if="result.items.length" class="table-container">
      <table class="table is-fullwidth is-hoverable">
        <thead>
          <tr>
            <th>When</th>
            <th>Action</th>
            <th>Movie</th>
            <th class="is-hidden-mobile">Details</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="entry in result.items" :key="entry.id">
            <td class="is-nowrap">
              <time :datetime="entry.occurredAt">{{ formatDateTime(entry.occurredAt) }}</time>
            </td>
            <td>
              {{ label(entry.type) }}
              <span v-if="entry.detail" class="is-hidden-tablet is-block is-size-7 has-text-grey">{{
                entry.detail
              }}</span>
            </td>
            <td>
              <RouterLink
                v-if="entry.movieId"
                :to="`/movies/${entry.movieId}`"
                class="has-text-weight-semibold"
                >{{ entry.movieTitle }}</RouterLink
              >
              <span v-else-if="entry.movieTitle" class="has-text-weight-semibold">{{
                entry.movieTitle
              }}</span>
            </td>
            <td class="is-hidden-mobile">{{ entry.detail }}</td>
          </tr>
        </tbody>
      </table>
    </div>
    <div v-else class="notification">
      Nothing yet. Log a viewing or add a movie to your watchlist.
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
