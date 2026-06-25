// src/frontend/mobile-app / src/api/client.ts
//
// API istemci konfigürasyonu.
// Axios instance — JWT token'ı her isteğe ekler.
//
// Akış:
//   1. AsyncStorage'dan token'ı oku
//   2. Her istekte Authorization: Bearer <token> header'ı ekle
//   3. 401 yanıtında token'ı sil ve login ekranına yönlendir

import axios from 'axios';
import AsyncStorage from '@react-native-async-storage/async-storage';

const API_BASE_URL = process.env.EXPO_PUBLIC_API_BASE_URL ?? 'http://localhost:8000';

export const apiClient = axios.create({
  baseURL: API_BASE_URL,
  timeout: 10000,
  headers: {
    'Content-Type': 'application/json',
  },
});

apiClient.interceptors.request.use(async (config) => {
  const token = await AsyncStorage.getItem('accessToken');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

apiClient.interceptors.response.use(
  (response) => response,
  async (error) => {
    if (error.response?.status === 401) {
      await AsyncStorage.multiRemove(['accessToken', 'refreshToken']);
    }
    return Promise.reject(error);
  }
);
