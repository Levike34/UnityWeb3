// SPDX-License-Identifier: MIT
pragma solidity ^0.8.0;

import "@openzeppelin/contracts/token/ERC20/extensions/ERC20Burnable.sol";
import "@openzeppelin/contracts/token/ERC20/ERC20.sol";
import "@openzeppelin/contracts/access/Ownable.sol";

contract Coin is ERC20Burnable, Ownable {

    mapping(address => uint) public spellcasts;
    mapping(address => uint) public hpPotions;
    mapping(address => uint) public mpPotions;

    constructor() ERC20("Coin", "COIN"){
        _mint(msg.sender, 100000000000000000000000000); //1,000,000,000 = 1 billion.
        spellcasts[msg.sender] = 5;
    } 

    function burn(uint256 amount) public override onlyOwner {
        _burn(_msgSender(), amount);
    }
    function mint(uint256 amount) public onlyOwner returns (bool success) {
        _mint(msg.sender, amount);
        return true;
    }

    function buyHp(uint256 amount) public onlyOwner {
        uint amountToPay = amount * 100;
        transfer(address(this), amountToPay);
        hpPotions[msg.sender] += amount;
    }

    function updateHp(uint256 amount) public onlyOwner {
        hpPotions[msg.sender] -= amount;
    }
        function buyMp(uint256 amount) public onlyOwner {
        uint amountToPay = amount * 100;
        transfer(address(this), amountToPay);
        mpPotions[msg.sender] += amount;
    }

    function updateMp(uint256 amount) public onlyOwner {
        mpPotions[msg.sender] -= amount;
    }
        function buySpellCasts(uint256 amount) public onlyOwner {
        uint amountToPay = amount * 100;
        transfer(address(this), amountToPay);
        spellcasts[msg.sender] += amount;
    }

    function updateSpellCasts(uint256 amount) public onlyOwner {
        spellcasts[msg.sender] -= amount;
    }

    function getItemBalance(uint256 choice) public view returns(uint256) {
        if(choice == 0) {
            return spellcasts[msg.sender];
        } else if(choice == 1) {
            return hpPotions[msg.sender];
        } else if(choice == 2) {
            return mpPotions[msg.sender];
        } else {
            return 0;
        }
    }
}
