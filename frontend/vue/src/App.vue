<script setup lang="ts">
import { ref } from 'vue';
import { useRoute } from 'vue-router';
import { useAuth } from './core/auth/auth';

const auth = useAuth();
const route = useRoute();
const user = auth.user;
const menuOpen = ref(false);

/** A section of the app is current on its own page and on all pages below it (/diary/new is in /diary). */
const isCurrent = (path: string) => route.path === path || route.path.startsWith(`${path}/`);

function logout(): void {
  menuOpen.value = false;
  auth.logout().catch((error: unknown) => console.error('Could not sign out', error));
}
</script>

<template>
  <nav v-if="user" class="navbar is-dark" aria-label="main navigation">
    <div class="container">
      <div class="navbar-brand">
        <RouterLink class="navbar-item has-text-weight-bold" to="/movies" @click="menuOpen = false">
          🎬&nbsp;Movie Tracker
        </RouterLink>
        <a
          role="button"
          class="navbar-burger"
          :class="{ 'is-active': menuOpen }"
          aria-label="menu"
          :aria-expanded="menuOpen"
          @click="menuOpen = !menuOpen"
        >
          <span aria-hidden="true"></span>
          <span aria-hidden="true"></span>
          <span aria-hidden="true"></span>
          <span aria-hidden="true"></span>
        </a>
      </div>

      <div class="navbar-menu" :class="{ 'is-active': menuOpen }">
        <div class="navbar-start" @click="menuOpen = false">
          <RouterLink
            class="navbar-item"
            to="/movies"
            :class="{ 'is-selected': isCurrent('/movies') }"
            >My movies</RouterLink
          >
          <RouterLink
            class="navbar-item"
            to="/diary"
            :class="{ 'is-selected': isCurrent('/diary') }"
            >Diary</RouterLink
          >
          <RouterLink
            class="navbar-item"
            to="/watchlist"
            :class="{ 'is-selected': isCurrent('/watchlist') }"
            >Watchlist</RouterLink
          >
          <RouterLink
            class="navbar-item"
            to="/activity"
            :class="{ 'is-selected': isCurrent('/activity') }"
            >Activity</RouterLink
          >
          <RouterLink
            class="navbar-item"
            to="/import"
            :class="{ 'is-selected': isCurrent('/import') }"
            >Import</RouterLink
          >
        </div>
        <div class="navbar-end">
          <div class="navbar-item">
            <RouterLink class="button is-primary" to="/diary/new" @click="menuOpen = false"
              >Log a viewing</RouterLink
            >
          </div>
          <div class="navbar-item">
            <img
              v-if="user.pictureUrl"
              class="mr-2"
              style="border-radius: 50%; max-height: 2rem"
              :src="user.pictureUrl"
              alt=""
              referrerpolicy="no-referrer"
            />
            <span class="mr-3" :title="user.email">{{ user.name }}</span>
            <button class="button is-small is-light is-outlined" @click="logout">Sign out</button>
          </div>
        </div>
      </div>
    </div>
  </nav>

  <main class="section">
    <div class="container">
      <!-- A page stays while only its parameters change, and is replaced by another page. -->
      <RouterView v-slot="{ Component, route: current }">
        <component :is="Component" :key="current.name" />
      </RouterView>
    </div>
  </main>

  <footer class="footer py-5">
    <div class="content has-text-centered is-size-7 has-text-grey">
      <!-- Required by TMDB's terms of use: https://www.themoviedb.org/api-terms-of-use -->
      <p>
        Movie data and posters from
        <a href="https://www.themoviedb.org/" target="_blank" rel="noopener">TMDB</a>. This product
        uses the TMDB API but is not endorsed or certified by TMDB.
      </p>
    </div>
  </footer>
</template>
