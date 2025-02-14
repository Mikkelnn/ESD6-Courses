clc; clear; close all;
syms X [3 1] matrixjthei  % Define symbolic variables

% Define matrix A and vector b
A = [1/5, -1/3; 1/20, 3/5];
b = [1; 4];

% Define function f(x)
f =x' * A' * A * x + b' * x;
diff(f,x,x')
% Compute gradient
g = gradient(f,x);

% Compute Hessian
H = hessian(f, x);

% Display results
disp('Gradient g(x):');
disp(g);
disp('Hessian Hf(x):');
disp(H);
