<template>
  <main class="section">
    <div class="container">
      <!-- A page stays while only its parameters change, and is replaced by another page. -->
      <RouterView v-slot="{ Component, route }">
        <component :is="Component" :key="route.name" />
      </RouterView>
    </div>
  </main>
</template>
