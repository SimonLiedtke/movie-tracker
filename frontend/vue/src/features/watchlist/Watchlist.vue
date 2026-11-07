<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useRouter } from 'vue-router';
import { useApi } from '../../core/api/api';
import { toApiError, type ApiError } from '../../core/api/api-error';
import type { MovieRef, PagedResponse, WatchlistItem } from '../../core/api/models';
import { yearOf } from '../../shared/dates';
import ErrorNotification from '../../shared/ErrorNotification.vue';
import MoviePicker from '../../shared/MoviePicker.vue';
import Pagination from '../../shared/Pagination.vue';
import Poster from '../../shared/Poster.vue';

const PAGE_SIZE = 20;

/** The page, from the route's query parameters. */
const props = defineProps<{
  page?: string;
}>();

const router = useRouter();
const api = useApi();

const page = computed(() => Number(props.page) || 1);

const result = ref<PagedResponse<WatchlistItem> | null>(null);
const loading = ref(true);
const error = ref<ApiError | null>(null);
/** Movie ID of the item whose note is being edited. */
const editing = ref<string | null>(null);
const noteDraft = ref('');
const saving = ref(false);

/** Numbers the requests, so that the answer to an outdated one is ignored. */
let latestLoad = 0;

/** Loads the current page of the watchlist. */
async function load(): Promise<void> {
  const thisLoad = ++latestLoad;
  loading.value = true;
  let loaded: PagedResponse<WatchlistItem> | null;
  try {
    loaded = await api.watchlist.list({ page: page.value, pageSize: PAGE_SIZE });
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

  // E.g. after removing the only item on the last page.
  if (loaded && loaded.items.length === 0 && loaded.page > 1) {
    goToPage(Math.max(1, Math.ceil(loaded.totalCount / loaded.pageSize)));
    return;
  }
  result.value = loaded;
  loading.value = false;
}

watch(page, load, { immediate: true });

function goToPage(target: number): void {
  router.push({ query: { page: target } });
}

/** Adds the movie at the top of the list (the newest come first). Already on it: nothing changes. */
async function add(movie: MovieRef): Promise<void> {
  try {
    // PUT replaces the note, so a movie that is already on the watchlist is left alone.
    const details = await api.movies.get(movie.id);
    if (!details.onWatchlist) {
      await api.watchlist.put(movie.id, null);
    }
  } catch (failure) {
    error.value = toApiError(failure);
    return;
  }
  if (result.value?.page === 1) {
    void load();
  } else {
    goToPage(1);
  }
}

function editNote(item: WatchlistItem): void {
  noteDraft.value = item.note ?? '';
  editing.value = item.movie.id;
}

async function saveNote(item: WatchlistItem, note: string): Promise<void> {
  saving.value = true;
  let updated: WatchlistItem;
  try {
    updated = await api.watchlist.put(item.movie.id, note.trim() || null);
  } catch (failure) {
    saving.value = false;
    error.value = toApiError(failure);
    return;
  }
  saving.value = false;
  editing.value = null;
  if (result.value) {
    result.value = {
      ...result.value,
      items: result.value.items.map((i) => (i.movie.id === updated.movie.id ? updated : i)),
    };
  }
}

async function remove(item: WatchlistItem): Promise<void> {
  try {
    await api.watchlist.remove(item.movie.id);
  } catch (failure) {
    error.value = toApiError(failure);
    return;
  }
  void load();
}
</script>

<template>
  <h1 class="title">Watchlist</h1>

  <div class="field">
    <label class="label" for="add">Add a movie</label>
    <MoviePicker
      input-id="add"
      placeholder="Search a movie to watch later"
      after-manual-add="watchlist"
      :keep-selection="false"
      @picked="add"
    />
  </div>

  <ErrorNotification :error="error" @dismiss="error = null" />

  <template v-if="result">
    <p class="mb-4 has-text-grey">
      {{ result.totalCount }} {{ result.totalCount === 1 ? 'movie' : 'movies' }} to watch
      <span v-if="loading" class="ml-2">· loading…</span>
    </p>

    <template v-if="result.items.length">
      <article v-for="item in result.items" :key="item.movie.id" class="media box">
        <figure class="media-left" style="width: 64px">
          <RouterLink :to="`/movies/${item.movie.id}`">
            <Poster :url="item.movie.posterUrl" :title="item.movie.title" />
          </RouterLink>
        </figure>
        <div class="media-content">
          <p>
            <RouterLink class="has-text-weight-semibold" :to="`/movies/${item.movie.id}`">{{
              item.movie.title
            }}</RouterLink>
            <span v-if="item.movie.releaseDate" class="has-text-grey">
              ({{ yearOf(item.movie.releaseDate) }})</span
            >
          </p>
          <p class="is-size-7 has-text-grey">Added {{ item.addedAt.slice(0, 10) }}</p>

          <form
            v-if="editing === item.movie.id"
            class="field has-addons mt-2"
            @submit.prevent="saveNote(item, noteDraft)"
          >
            <div class="control is-expanded">
              <input
                v-model="noteDraft"
                class="input is-small"
                maxlength="500"
                placeholder="Why do you want to watch it?"
                @keydown.escape="editing = null"
              />
            </div>
            <div class="control">
              <button
                type="submit"
                class="button is-small is-primary"
                :class="{ 'is-loading': saving }"
              >
                Save
              </button>
            </div>
            <div class="control">
              <button type="button" class="button is-small" @click="editing = null">Cancel</button>
            </div>
          </form>
          <p v-else-if="item.note" class="mt-2 pre-wrap">{{ item.note }}</p>
        </div>
        <div class="media-right">
          <div class="buttons are-small is-right">
            <RouterLink
              class="button is-primary"
              :to="{ path: '/diary/new', query: { movieId: item.movie.id } }"
            >
              Log a viewing
            </RouterLink>
            <button class="button" @click="editNote(item)">
              {{ item.note ? 'Edit note' : 'Add note' }}
            </button>
            <button class="button is-danger is-outlined" @click="remove(item)">Remove</button>
          </div>
        </div>
      </article>
    </template>
    <div v-else class="notification">Your watchlist is empty. Search a movie above to add it.</div>

    <Pagination
      :page="result.page"
      :page-size="result.pageSize"
      :total-count="result.totalCount"
      @page-change="goToPage"
    />
  </template>
  <progress v-else-if="loading" class="progress is-small is-primary" max="100"></progress>
</template>
