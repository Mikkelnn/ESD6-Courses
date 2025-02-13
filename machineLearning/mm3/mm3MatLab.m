clc; clear; close all;

% Define grid range
[x1, x2] = meshgrid(linspace(-6, 6, 1000), linspace(-10, 2, 1000));

% Compute the Euclidean distance d from transmitter (-2, -4)
d1 = sqrt((x1 + 2).^2 + (x2 + 4).^2);
d2 = sqrt((x1 + 2).^2 + (x2 + 4).^2);

% Constraints
C1 = -x1.^2 - (x2 + 4).^2 + 16;  % Inside the circle
C2 = x1 - x2 - 6;  % Half-plane
C3 = x1.^2 + (x2 + 6).^2 - 2;  % Outside the small circle

% Feasibility mask
feasible_region1 = (C1 >= 0) & (C2 >= 0);
feasible_region2 = (C1 >= 0) & (C2 >= 0) & (C3 >= 0);

% Set infeasible regions to NaN to hide them
d1(~feasible_region1) = NaN;
d2(~feasible_region2) = NaN;

% Create 3D plot
figure;
surf(x1, x2, d1, 'FaceAlpha', 0.7, 'EdgeColor', 'none');
colormap('parula'); % Change colormap if needed
hold on;

% Labels and title
xlabel('x_1');
ylabel('x_2');
zlabel('Distance d1');
title('3D Plot of Feasible Distance d1');
colorbar;
grid on;
view(3);
hold off

figure;
surf(x1, x2, d2, 'FaceAlpha', 0.7, 'EdgeColor', 'none');
colormap('parula'); % Change colormap if needed
hold on;

% Labels and title
xlabel('x_1');
ylabel('x_2');
zlabel('Distance d2');
title('3D Plot of Feasible Distance d2');
colorbar;
grid on;
view(3);