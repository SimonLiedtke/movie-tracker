import { createWebHistory } from 'vue-router';
import { startApp } from './create-app';
import './styles.scss';

startApp(createWebHistory(), '#app').catch((err) => console.error(err));
