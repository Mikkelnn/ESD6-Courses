import numpy as np
import matplotlib.pyplot as plt

# === Load and clean the data ===
data = np.loadtxt("co2_weekly_mlo.txt")
t = data[:, 3]  # decimal year
co2 = data[:, 4]
valid = co2 != -999.99
t = t[valid]
co2 = co2[valid]

# === Train/Test split ===
split_index = len(t) // 2
t_train, t_test = t[:split_index], t[split_index:]
co2_train, co2_test = co2[:split_index], co2[split_index:]

# Normalize time for numerical stability
t_mean, t_std = t_train.mean(), t_train.std()
t_train_std = (t_train - t_mean) / t_std
t_test_std = (t_test - t_mean) / t_std
t_std_all = (t - t_mean) / t_std

# === Create basis functions ===
def create_basis(t_scaled, num_sin):
    t_scaled = np.asarray(t_scaled)
    Phi = [np.ones_like(t_scaled), t_scaled]
    for k in range(1, num_sin + 1):
        Phi.append(np.sin(k * np.pi * t_scaled))  # scaled sin terms
    return np.column_stack(Phi)

# === Ridge regression ===
def ridge_regression(Phi, y, lam):
    I = np.eye(Phi.shape[1])
    return np.linalg.inv(Phi.T @ Phi + lam * I) @ Phi.T @ y

# === Lasso via subgradient descent ===
def lasso_regression(Phi, y, lam, lr=1e-3, epochs=10000):
    w = np.zeros(Phi.shape[1])
    for _ in range(epochs):
        grad = Phi.T @ (Phi @ w - y) + lam * np.sign(w)
        w -= lr * grad
    return w

# === R^2 score ===
def r2_score(y_true, y_pred):
    ss_res = np.sum((y_true - y_pred) ** 2)
    ss_tot = np.sum((y_true - np.mean(y_true)) ** 2)
    return 1 - ss_res / ss_tot

# === Parameters ===
lambd = 0
sine_counts = [0, 5, 10, 20, 50, 99]  # Includes sin=0 case

# === Plotting ===
fig, axs = plt.subplots(3, 2, figsize=(16, 12))
axs = axs.flatten()

for i, num_sin in enumerate(sine_counts):
    # Design matrices
    Phi_train = create_basis(t_train_std, num_sin)
    Phi_test = create_basis(t_test_std, num_sin)
    Phi_all = create_basis(t_std_all, num_sin)

    # Train models
    w_ridge = ridge_regression(Phi_train, co2_train, lambd)
    w_lasso = lasso_regression(Phi_train, co2_train, lam=lambd)

    # Predict on full and test set
    co2_pred_ridge = Phi_all @ w_ridge
    co2_pred_lasso = Phi_all @ w_lasso
    co2_test_pred_ridge = Phi_test @ w_ridge
    co2_test_pred_lasso = Phi_test @ w_lasso

    # R^2 scores
    r2_ridge = r2_score(co2_test, co2_test_pred_ridge)
    r2_lasso = r2_score(co2_test, co2_test_pred_lasso)

    # Plot
    axs[i].plot(t, co2, 'k-', label='True CO2')
    axs[i].plot(t, co2_pred_ridge, 'r-', label=f'Ridge (sin={num_sin})')
    axs[i].plot(t, co2_pred_lasso, 'g--', label=f'Lasso (sin={num_sin})')
    axs[i].axvline(t[split_index], color='gray', linestyle=':', label='Train/Test Split' if i == 0 else "")
    axs[i].set_title(f"sin={num_sin} | R² Ridge={r2_ridge:.4f}, Lasso={r2_lasso:.4f}")
    axs[i].set_xlabel("Year")
    axs[i].set_ylabel("CO₂ ppm")
    axs[i].legend()

plt.tight_layout()
plt.show()
