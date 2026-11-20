import { reactive } from 'vue'
import { api } from './api'

/**
 * 登录状态，整个面板共享
 */
export const auth = reactive({
  loaded: false,
  initialized: false,
  authenticated: false,
  user: null
})

/**
 * 读取面板登录状态
 */
export async function refreshAuth() {
  const res = await api.get('auth/state')
  const data = res.data || {}

  auth.initialized = !!data.initialized
  auth.authenticated = !!data.authenticated
  auth.user = data.user || null
  auth.loaded = true
}

/**
 * 清掉本地登录状态，服务端 cookie 已经失效或撤销时调用
 */
export function clearAuth() {
  auth.authenticated = false
  auth.user = null
}

/**
 * 登录
 * @param {string} user 用户名
 * @param {string} password 密码
 */
export async function login(user, password) {
  const res = await api.post('auth/login', { user, password })

  auth.initialized = true
  auth.authenticated = true
  auth.user = (res.data && res.data.user) || null

  return res
}

/**
 * 创建第一个账户，成功后直接登录
 * @param {string} user 用户名
 * @param {string} password 密码
 */
export async function initAccount(user, password) {
  const res = await api.post('auth/init', { user, password })

  auth.initialized = true
  auth.authenticated = true
  auth.user = (res.data && res.data.user) || null

  return res
}

/**
 * 退出登录
 */
export async function logout() {
  try {
    await api.post('auth/logout')
  } catch {
    // 会话可能已经过期，本地状态照样清掉
  }

  clearAuth()
}
