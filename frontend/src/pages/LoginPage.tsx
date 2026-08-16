import { type FormEvent, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuthStore } from '../store/authStore'

export function LoginPage() {
  const login = useAuthStore((state) => state.login)
  const error = useAuthStore((state) => state.error)
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSubmitting(true)
    try {
      await login(email, password)
      navigate('/')
    } catch {
      // L'erreur est déjà exposée par le store (state.error), affichée ci-dessous.
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <section className="mx-auto max-w-sm rounded-card border border-border bg-white p-5 shadow-card">
      <h1 className="mb-4 text-2xl font-semibold text-text">Connexion</h1>
      <form onSubmit={handleSubmit} noValidate>
        <div className="mb-4">
          <label htmlFor="login-email" className="mb-1 block text-sm text-text">
            Adresse e-mail
          </label>
          <input
            id="login-email"
            name="email"
            type="email"
            autoComplete="email"
            required
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            className="w-full rounded-button border border-border px-3 py-2 text-text"
          />
        </div>
        <div className="mb-4">
          <label htmlFor="login-password" className="mb-1 block text-sm text-text">
            Mot de passe
          </label>
          <input
            id="login-password"
            name="password"
            type="password"
            autoComplete="current-password"
            required
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            className="w-full rounded-button border border-border px-3 py-2 text-text"
          />
        </div>
        {error && (
          <p role="alert" className="mb-4 text-sm text-red">
            {error}
          </p>
        )}
        <button
          type="submit"
          disabled={submitting}
          className="w-full rounded-button bg-blue-maat px-4 py-2 font-medium text-white shadow-button disabled:opacity-60"
        >
          Se connecter
        </button>
      </form>
      <p className="mt-4 text-sm text-text-muted">
        Pas encore de compte ? <Link to="/register" className="text-blue-maat-text">Créer un compte</Link>
      </p>
    </section>
  )
}
