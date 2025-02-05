clc; clear; close all;

% Define transmitter location
xt = [-2, -4];

% Define grid for visualization
[x1, x2] = meshgrid(-6:0.1:6, -6:0.1:6);

% Define the constraints
c1 = -(x1.^2 + (x2 + 4).^2 - 16);  % c1 >= 0
c2 = x1 - x2 - 6;  % c2 >= 0

% Compute distance squared from transmitter
d2 = (x1 - xt(1)).^2 + (x2 - xt(2)).^2;
P = 1 ./ d2; % Signal power is inversely proportional to distance squared

% Plot feasible region
figure; hold on; axis equal;
contourf(x1, x2, (c1 >= 0) & (c2 >= 0), [0.5 0.5], 'g', 'LineWidth', 2);
xlabel('x_1'); ylabel('x_2'); title('Feasible Region and Power Contours');
colormap([0.8 1 0.8]); % Light green for feasible region
grid on;

% Plot constraint boundaries
fimplicit(@(x1, x2) x1 - x2 - 6, [-6, 6, -6, 6], 'r', 'LineWidth', 2); % Line constraint
fimplicit(@(x1, x2) x1.^2 + (x2 + 4).^2 - 16, [-6, 6, -6, 6], 'b', 'LineWidth', 2); % Circular constraint

% Plot power contours (objective function)
contour(x1, x2, P, 50, 'k'); 

% Solve the problem graphically by finding the optimal point
syms x1_opt x2_opt
eq1 = x1_opt - x2_opt - 6; % Line constraint
eq2 = x1_opt^2 + (x2_opt + 4)^2 - 16; % Circle constraint

sol = vpasolve([eq1 == 0, eq2 == 0], [x1_opt, x2_opt]); % Solve numerically

% Convert symbolic solutions to numeric values
x_opt1 = double(sol.x1_opt);
x_opt2 = double(sol.x2_opt);

% If multiple solutions exist, choose the best one based on distance to transmitter
if length(x_opt1) > 1
    distances = (x_opt1 - xt(1)).^2 + (x_opt2 - xt(2)).^2; % Compute squared distances
    [~, idx] = min(distances); % Choose the closest point
    x_opt = [x_opt1(idx), x_opt2(idx)];
else
    x_opt = [x_opt1, x_opt2];
end

% Mark the optimal solution
plot(x_opt(1), x_opt(2), 'mo', 'MarkerSize', 10, 'LineWidth', 2, 'MarkerFaceColor', 'm');
plot(xt(1), xt(2), 'ks', 'MarkerSize', 10, 'LineWidth', 2, 'MarkerFaceColor', 'k'); % Mark transmitter

legend({'Feasible Region', 'x1 - x2 - 6 = 0', '(x1+4)^2 + (x2+4)^2 = 16', 'Power Contours', 'Optimal Solution', 'Transmitter'}, 'Location', 'best');

hold off;
