library IEEE;
use IEEE.STD_LOGIC_1164.ALL;
use IEEE.STD_LOGIC_ARITH.ALL;
use IEEE.STD_LOGIC_UNSIGNED.ALL;

entity MatrixMultiplier_TB is
end MatrixMultiplier_TB;

architecture testbench of MatrixMultiplier_TB is
    constant N : integer := 4;
    signal clk   : std_logic := '0';
    signal start : std_logic := '0';
    signal done  : std_logic;
    signal A, B  : std_logic_vector((N*N*8)-1 downto 0);
    signal C     : std_logic_vector((N*N*16)-1 downto 0);

    component MatrixMultiplierParallel
        generic (N : integer := 4);
        port (
            clk   : in std_logic;
            start : in std_logic;
            A, B  : in std_logic_vector((N*N*8)-1 downto 0);
            C     : out std_logic_vector((N*N*16)-1 downto 0);
            done  : out std_logic
        );
    end component;

begin
    -- Clock process
    process
    begin
        while true loop
            clk <= '0';
            wait for 42 ns;
            clk <= '1';
            wait for 42 ns;
        end loop;
    end process;
    
    -- Stimulus process
    process
    begin
        -- Initialize matrices as a flattened vector
        A <= "00000001000000100000001100000100" &
             "00000101000001100000011100001000" &
             "00001001000010100000101100001100" &
             "00001101000011100000111100010000";
        
        B <= "00000001000000100000001100000100" &
             "00000101000001100000011100001000" &
             "00001001000010100000101100001100" &
             "00001101000011100000111100010000";
        
        -- Start multiplication
        start <= '1';
        wait for 84 ns;
        start <= '0';
        
        -- Wait for completion
        wait until done = '1';
        
        -- Stop simulation
        wait;
    end process;
    
    -- Instantiate the matrix multiplier
    uut: MatrixMultiplierParallel
        generic map (N => 4)
        port map (
            clk   => clk,
            start => start,
            A     => A,
            B     => B,
            C     => C,
            done  => done
        );
end testbench;
