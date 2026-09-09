import axios from 'axios';
const api = axios.create({ baseURL: import.meta.env.VITE_API_URL || 'http://localhost:5183/api', timeout: 15000 });
api.interceptors.request.use(config=>{const token=localStorage.getItem('token'); if(token) config.headers.Authorization=`Bearer ${token}`; return config;});
api.interceptors.response.use(r=>r,e=>{if(e.response?.status===401){localStorage.clear(); window.location.href='/';} return Promise.reject(e);});
export default api;
