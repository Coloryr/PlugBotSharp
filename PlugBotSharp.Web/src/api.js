const BASE = '/api/'

let unauthorizedHandler = null

/**
 * 设置 401 回调，用来在会话失效时退回登录页
 * @param {Function} handler 回调
 */
export function setUnauthorizedHandler(handler) {
  unauthorizedHandler = handler
}

/**
 * 发起请求，统一解析 { ok, message, data } 结构
 * @param {string} path 接口路径，去掉开头的 /api/
 * @param {RequestInit} options fetch 参数
 * @returns {Promise<{ok: boolean, message: string, data: any}>}
 */
async function request(path, options = {}) {
  let res

  try {
    res = await fetch(BASE + path, { cache: 'no-store', ...options })
  } catch (e) {
    throw new Error('无法连接核心: ' + e.message)
  }

  const text = await res.text()
  let body = null

  if (text) {
    try {
      body = JSON.parse(text)
    } catch {
      body = null
    }
  }

  // 登录失效，交给上层切回登录页
  if (res.status === 401 && unauthorizedHandler) {
    unauthorizedHandler()
  }

  if (!res.ok || (body && body.ok === false)) {
    throw new Error((body && body.message) || 'HTTP ' + res.status)
  }

  return body || { ok: true, message: '', data: null }
}

export const api = {
  /** 读取接口 */
  get(path) {
    return request(path)
  },

  /** 提交 json */
  post(path, body) {
    return request(path, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body ?? {})
    })
  },

  /** 删除 */
  del(path) {
    return request(path, { method: 'DELETE' })
  },

  /** 上传文件 */
  upload(path, file) {
    const form = new FormData()
    form.append('file', file, file.name)

    return request(path, { method: 'POST', body: form })
  }
}
